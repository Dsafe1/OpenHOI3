// Applies the function names recovered so far by manual analysis of hoi3_tfh.exe.
//
// ReconstructClassesFromRtti recovers everything RTTI can prove: class namespaces,
// vftables and which class introduced each virtual slot. It cannot name anything,
// because the binary carries no symbols. This script fills in the names worked out by
// reading the code, and records for each one the evidence and how much to trust it.
//
// Run it after ReconstructClassesFromRtti so the class namespaces already exist.
// Re-running is safe: names set by hand in the GUI are never overwritten.
//
// Addresses are image-base relative, so this still works if the program is rebased.
//
//@author OpenHOI3
//@category C++
//@keybinding
//@menupath
//@toolbar

import java.util.ArrayList;
import java.util.List;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.GhidraClass;
import ghidra.program.model.symbol.Namespace;
import ghidra.program.model.symbol.SourceType;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolTable;

public class ApplyKnownNames extends GhidraScript {

	/// How far to trust a name.
	private enum Confidence {
		/// Behaviour is unambiguous from the code itself.
		CERTAIN,
		/// Strongly implied by a log string, vtable slot or call pattern.
		LIKELY,
		/// Consistent with the evidence, but the exact original name is a guess.
		TENTATIVE,
	}

	private record Known(long rva, String className, String name, Confidence confidence,
		String evidence) {
	}

	/// Everything identified so far. Extend as more is worked out.
	private static final Known[] KNOWN = {

		// ---- CFrontEnd -----------------------------------------------------
		new Known(0x2eef00L, "CFrontEnd", "Enter", Confidence.LIKELY,
			"Vtable slot 3. Logs \"entering frontend...\" and, further in, " +
			"\"Initialising frontend artwork <\". Both belong to this one function: " +
			"there is no padding or call target between them."),

		new Known(0x2edd60L, "CFrontEnd", "Update", Confidence.TENTATIVE,
			"Vtable slot 1, so the first non-destructor virtual of the CIdler chain. " +
			"Starts and stops the menu song against a timer and re-hides nudge_button, " +
			"which is per-frame work rather than one-time setup."),

		// ---- CFrontEndState, the main menu view ----------------------------
		new Known(0x2fc8b0L, "CFrontEndState", "Activate", Confidence.TENTATIVE,
			"Vtable slot 2. Shows its own window, then resolves nudge_button and calls " +
			"Hide on it. This is why the map editor button never appears in retail."),

		new Known(0x2fbb30L, "CFrontEndState", "CFrontEndState", Confidence.LIKELY,
			"Constructor. Its only call site is the standard MSVC new sequence: " +
			"allocate, call, jump over a xor eax,eax null path, then store the " +
			"resulting pointer into CFrontEnd+0x6a0. It writes its arguments into " +
			"members and attaches the observer glue to each main menu button via slot " +
			"+0x4 of their CButtonObservable subobject, which is what identifies +0x4 " +
			"as attach. Matches VCFrontEndState::__CButtonObserverGlue in the RTTI."),

		new Known(0x2fc400L, "CFrontEndState", "DetachButtonObservers",
			Confidence.TENTATIVE,
			"Mirror of the constructor's observer wiring, calling slot +0x8 on the " +
			"same CButtonObservable subobjects. Since the constructor establishes +0x4 " +
			"as attach, +0x8 is the detach side. Whether this is the destructor itself " +
			"or a helper it calls has not been established."),

		// ---- GUI framework -------------------------------------------------
		new Known(0x27abc0L, "CEU3Minimap", "Hide", Confidence.CERTAIN,
			"Vtable slot 14 (+0x38) of the CGuiObject chain. Clears a visibility byte " +
			"at +0x43, zeroes a float at +0xac, then calls the same slot on its child " +
			"objects. Clearing a flag and recursing into children is a hide."),

		new Known(0x6c2e80L, "CEU3DialogGui", "FindObject", Confidence.CERTAIN,
			"Vtable slot 13 (+0x34) of the TWindow chain. Takes a name and compares it " +
			"against child name fields, returning the matching child. This is the " +
			"lookup the front end uses to reach controls by name."),

		// ---- CRT ------------------------------------------------------------
		new Known(0xa160L, "", "std_string_assign", Confidence.CERTAIN,
			"MSVC std::basic_string<char>::assign(const char*, size_t). Compares the " +
			"capacity field at +0x14 against 0x10, the small-string optimisation " +
			"threshold, and only then dereferences +0x0 as a heap buffer."),

		new Known(0x795f9bL, "", "crt_free", Confidence.LIKELY,
			"Hot-patch thunk tail-jumping to the real deallocator. Called cdecl with " +
			"one pointer to release std::string heap buffers."),
	};

	private int named;
	private int skipped;
	private int missing;

	@Override
	public void run() throws Exception {
		if (currentProgram == null) {
			println("No program open.");
			return;
		}

		long imageBase = currentProgram.getImageBase().getOffset();
		println("Image base 0x" + Long.toHexString(imageBase));

		var report = new ArrayList<String>();
		for (Known known : KNOWN) {
			monitor.checkCancelled();
			apply(known, imageBase, report);
		}

		println("");
		println("=== applied names ===");
		for (String line : report) {
			println("  " + line);
		}
		println("");
		println("named: " + named + ", left alone: " + skipped + ", not found: " + missing);
	}

	private void apply(Known known, long imageBase, List<String> report) throws Exception {
		Address address = toAddr(imageBase + known.rva());

		Function function = getFunctionAt(address);
		if (function == null) {
			// An address worked out by hand can land inside a function rather than on
			// its entry, so trust the analysed boundaries over the recorded address.
			function = getFunctionContaining(address);
		}
		if (function == null && looksLikeFunctionStart(address)) {
			// Auto-analysis does not always reach a target, and a hot-patch thunk in
			// particular often ends up as raw bytes.
			disassemble(address);
			function = createFunction(address, null);
		}
		if (function == null) {
			report.add(String.format("%s  NO FUNCTION - %s not applied", address,
				known.name()));
			missing++;
			return;
		}

		Address entry = function.getEntryPoint();
		String resolved = entry.equals(address) ? "" : " (entry " + entry + ")";

		Symbol symbol = function.getSymbol();
		if (symbol != null && symbol.getSource() == SourceType.USER_DEFINED) {
			report.add(String.format("%s  kept your name '%s' (would have been %s)",
				address, function.getName(), known.name()));
			skipped++;
			return;
		}

		if (!known.className().isEmpty()) {
			GhidraClass owner = ensureClass(known.className());
			if (owner != null && function.getParentNamespace() != owner) {
				function.setParentNamespace(owner);
			}
		}
		function.setName(known.name(), SourceType.ANALYSIS);

		setPlateComment(entry, known.name() + "  [" + known.confidence() + "]\n" +
			known.evidence());

		String label = known.className().isEmpty() ? known.name()
			: known.className() + "::" + known.name();
		report.add(String.format("%s  %-46s %s%s", address, label, known.confidence(),
			resolved));
		named++;
	}

	/**
	 * Guards against inventing a function in the middle of an existing one.
	 *
	 * An address worked out by reading code can easily land mid-body, and creating a
	 * function there silently corrupts the analysis. MSVC starts a function either
	 * after int3 alignment padding or immediately after the previous function's
	 * return, so anything else is treated as not a function start.
	 *
	 * A run of int3 is required rather than a single one, because 0xCC also occurs
	 * inside ordinary instructions - "mov ecx, esp" is 8B CC, and its trailing byte
	 * is enough to make a lone-byte check accept an address mid-function.
	 */
	private boolean looksLikeFunctionStart(Address address) {
		try {
			if (getByte(address.subtract(1)) == (byte) 0xC3) {
				return true;                          // straight after a ret
			}
			if (getByte(address.subtract(3)) == (byte) 0xC2) {
				return true;                          // straight after a ret imm16
			}
			return getByte(address.subtract(1)) == (byte) 0xCC
				&& getByte(address.subtract(2)) == (byte) 0xCC
				&& getByte(address.subtract(3)) == (byte) 0xCC;
		}
		catch (Exception e) {
			return false;
		}
	}

	/// Reuses the namespace ReconstructClassesFromRtti made, or creates it.
	private GhidraClass ensureClass(String name) throws Exception {
		SymbolTable symbols = currentProgram.getSymbolTable();
		Namespace global = currentProgram.getGlobalNamespace();
		Namespace existing = symbols.getNamespace(name, global);
		if (existing instanceof GhidraClass found) {
			return found;
		}
		if (existing != null) {
			return null;                              // occupied by a plain namespace
		}
		return symbols.createClass(global, name, SourceType.ANALYSIS);
	}
}
