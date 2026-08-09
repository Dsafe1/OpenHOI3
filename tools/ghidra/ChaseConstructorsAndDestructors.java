// Finds the constructors and destructors of every class recovered from RTTI.
//
// The signal is that only a constructor or a destructor writes a class's vftable
// pointer into the object. Scanning code for writes of each known vftable address
// therefore yields exactly those functions, and nothing else.
//
// Telling the two apart uses the MSVC layout: when a class has a virtual destructor,
// vftable slot 0 holds the scalar deleting destructor, which calls the real one. So
// the vftable writer that slot 0 calls is the destructor, and the rest are
// constructors. Where that link cannot be established the functions are reported as
// unclassified rather than guessed at.
//
// Function boundaries come from Ghidra's analysis rather than from scanning for
// padding, so run this after auto-analysis. Run it after ReconstructClassesFromRtti
// too, so the class namespaces already exist.
//
//@author OpenHOI3
//@category C++
//@keybinding
//@menupath
//@toolbar

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.GhidraClass;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.scalar.Scalar;
import ghidra.program.model.symbol.Namespace;
import ghidra.program.model.symbol.SourceType;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolTable;

public class ChaseConstructorsAndDestructors extends GhidraScript {

	private static final int MAX_VFTABLE_SLOTS = 4096;

	/// How far back to look for the opcode that owns an immediate operand.
	private static final int OPCODE_LOOKBACK = 6;

	// ---- model --------------------------------------------------------------

	private static final class RttiClass {
		String mangled = "";
		String name = "";
		long chd;
		final List<RttiClass> directBases = new ArrayList<>();
		final Set<RttiClass> allAncestors = new LinkedHashSet<>();
		final TreeMap<Integer, Long> vftables = new TreeMap<>();   // offset -> address
		GhidraClass namespace;

		@Override
		public String toString() {
			return name;
		}
	}

	private static final class Blk {
		long start;
		long end;
		byte[] data;
		boolean exec;
	}

	private final List<Blk> blocks = new ArrayList<>();
	private final List<Blk> dataBlocks = new ArrayList<>();
	private final Map<Long, RttiClass> classesByTd = new HashMap<>();
	private final Set<Long> colPointerSites = new HashSet<>();

	private int constructors;
	private int destructors;
	private int deleting;
	private int unclassified;
	private int ambiguous;
	private int shared;
	private int kept;

	// =========================================================================

	@Override
	public void run() throws Exception {
		if (currentProgram == null || currentProgram.getDefaultPointerSize() != 4) {
			println("Needs a 32-bit MSVC program.");
			return;
		}

		loadBlocks();
		Set<Long> typeDescriptors = findTypeDescriptors();
		List<Long> cols = findCompleteObjectLocators(typeDescriptors);
		buildClasses(cols);
		resolveHierarchy();
		locateVftables(cols);
		println("Classes: " + classesByTd.size());

		// vftable address -> owning class
		Map<Long, RttiClass> owners = new HashMap<>();
		for (RttiClass c : classesByTd.values()) {
			for (long address : c.vftables.values()) {
				owners.put(address, c);
			}
		}
		println("Vftables: " + owners.size());

		Map<Function, Set<RttiClass>> writers = findVftableWriters(owners);
		println("Functions writing a vftable: " + writers.size());

		Map<RttiClass, Set<Function>> byClass = attribute(writers);
		classifyAndName(byClass);

		println("");
		println("=== summary ===");
		println("  constructors named          : " + constructors);
		println("  destructors named           : " + destructors);
		println("  scalar deleting destructors : " + deleting);
		println("  classes with no virtual dtor: " + unclassified);
		println("  left alone (your names)     : " + kept);
		println("  skipped, owner ambiguous    : " + ambiguous
			+ "  (write vftables of unrelated hierarchies)");
		println("  functions shared by classes : " + shared
			+ "  (COMDAT-folded; extra labels added per class)");
	}

	// ---- step 1: which functions write a vftable ---------------------------

	/**
	 * Single pass over executable memory looking for any known vftable address used
	 * as an immediate. Doing it in one sweep matters: there are thousands of
	 * vftables, and searching for each separately would mean re-reading the text
	 * section thousands of times.
	 */
	private Map<Function, Set<RttiClass>> findVftableWriters(Map<Long, RttiClass> owners)
			throws Exception {
		var result = new HashMap<Function, Set<RttiClass>>();
		monitor.setMessage("Scanning code for vftable writes");

		for (Blk b : blocks) {
			if (!b.exec) {
				continue;
			}
			monitor.checkCancelled();
			byte[] d = b.data;
			for (int i = 0; i + 4 <= d.length; i++) {
				long value = u32(readInt(b, i));
				RttiClass owner = owners.get(value);
				if (owner == null || !ownsImmediate(d, i)) {
					continue;
				}
				Function function = getFunctionContaining(toAddr(b.start + i));
				if (function != null) {
					result.computeIfAbsent(function, k -> new LinkedHashSet<>()).add(owner);
				}
			}
		}
		return result;
	}

	/**
	 * Checks the bytes before a candidate immediate for an instruction that would
	 * actually take one, so that an address appearing incidentally is not counted.
	 * Constructors overwhelmingly use "mov dword ptr [reg+disp], imm32" (0xC7); the
	 * "mov reg, imm32" form (0xB8..0xBF) shows up when the value is reused.
	 */
	private static boolean ownsImmediate(byte[] d, int immediateOffset) {
		int previous = immediateOffset - 1;
		if (previous >= 0) {
			int op = d[previous] & 0xFF;
			if (op >= 0xB8 && op <= 0xBF) {
				return true;
			}
		}
		for (int back = 1; back <= OPCODE_LOOKBACK; back++) {
			int at = immediateOffset - back;
			if (at < 0) {
				break;
			}
			if ((d[at] & 0xFF) == 0xC7) {
				return true;
			}
		}
		return false;
	}

	// ---- step 2: which class does a writer belong to ------------------------

	/**
	 * A constructor may write several vftables: its own, and those of base
	 * subobjects whose constructors were inlined into it. The owner is then the most
	 * derived of them, which the RTTI hierarchy already tells us. Anything that
	 * writes vftables from unrelated hierarchies is left out rather than guessed at.
	 */
	private Map<RttiClass, Set<Function>> attribute(Map<Function, Set<RttiClass>> writers) {
		var byClass = new HashMap<RttiClass, Set<Function>>();
		for (Map.Entry<Function, Set<RttiClass>> entry : writers.entrySet()) {
			Set<RttiClass> candidates = entry.getValue();
			RttiClass owner = null;
			for (RttiClass candidate : candidates) {
				boolean derivedFromAll = true;
				for (RttiClass other : candidates) {
					if (other != candidate && !candidate.allAncestors.contains(other)) {
						derivedFromAll = false;
						break;
					}
				}
				if (derivedFromAll) {
					owner = candidate;
					break;
				}
			}
			if (owner != null) {
				byClass.computeIfAbsent(owner, k -> new LinkedHashSet<>()).add(entry.getKey());
			}
			else {
				ambiguous++;
			}
		}
		return byClass;
	}

	// ---- step 3: classify and name ------------------------------------------

	/// One class's claim on a function. Several classes can claim the same one.
	private record Claim(Function function, RttiClass owner, String name, String kind) {
	}

	private void classifyAndName(Map<RttiClass, Set<Function>> byClass) throws Exception {
		List<RttiClass> ordered = new ArrayList<>(byClass.keySet());
		ordered.sort((a, b) -> a.name.compareToIgnoreCase(b.name));

		var claims = new ArrayList<Claim>();

		monitor.initialize(ordered.size());
		for (RttiClass c : ordered) {
			monitor.checkCancelled();
			monitor.incrementProgress(1);

			Set<Function> found = byClass.get(c);
			c.namespace = ensureClass(c.name);
			if (c.namespace == null) {
				continue;
			}

			// Slot 0 of the primary vftable holds the scalar deleting destructor, but
			// only when the destructor is virtual. Roughly a sixth of these classes
			// have something else there, so the shape is checked before trusting it.
			Function deletingDtor = null;
			Long primary = c.vftables.get(0);
			if (primary != null) {
				Function slotZero = getFunctionAt(toAddr(u32(readInt(primary))));
				if (slotZero != null && looksLikeDeletingDestructor(slotZero)) {
					deletingDtor = slotZero;
				}
			}

			// A destructor only rewrites the vftable when the compiler needs it to,
			// so the real destructor is found through the deleting one rather than by
			// assuming it appears among the writers.
			Function dtor = null;
			if (deletingDtor != null) {
				Set<Function> called = deletingDtor.getCalledFunctions(monitor);
				for (Function candidate : found) {
					if (called.contains(candidate)) {
						dtor = candidate;
						break;
					}
				}
			}

			if (deletingDtor != null) {
				if (dtor != null) {
					claims.add(new Claim(dtor, c, "~" + c.name, "destructor"));
					claims.add(new Claim(deletingDtor, c, "~" + c.name + "_scalar_deleting",
						"scalar deleting destructor"));
				}
				else {
					// The body was inlined into the deleting form, or does not write
					// the vftable. Either way this is the destructor entry point.
					claims.add(new Claim(deletingDtor, c, "~" + c.name,
						"destructor (deleting form)"));
				}
			}
			else {
				unclassified++;               // no virtual destructor to key off
			}

			// Whatever is left writes the vftable without being the destructor, which
			// leaves only constructors.
			int index = 0;
			for (Function f : found) {
				if (f.equals(dtor) || f.equals(deletingDtor)) {
					continue;
				}
				index++;
				claims.add(new Claim(f, c, index == 1 ? c.name : c.name + "_" + index,
					"constructor"));
			}
		}

		applyClaims(claims);
	}

	/**
	 * Turns claims into symbols, coping with one function being claimed by several
	 * classes.
	 *
	 * This is the norm rather than the exception here: identical COMDAT folding merges
	 * destructors that compile to the same code, so barely 500 distinct functions serve
	 * every class in the binary. Renaming per class would leave whichever class sorted
	 * last owning the symbol and the rest silently pointing at someone else's name.
	 *
	 * Instead the function gets one primary name and an extra label per other claimant,
	 * inside that claimant's own class namespace, so every class can still be navigated
	 * to. Where one claimant is an ancestor of all the others the base class wins the
	 * primary name, since a folded destructor shared down a hierarchy is really the
	 * base's.
	 */
	private void applyClaims(List<Claim> claims) throws Exception {
		var byFunction = new HashMap<Function, List<Claim>>();
		for (Claim claim : claims) {
			byFunction.computeIfAbsent(claim.function(), k -> new ArrayList<>()).add(claim);
		}

		SymbolTable symbols = currentProgram.getSymbolTable();
		monitor.initialize(byFunction.size());
		for (Map.Entry<Function, List<Claim>> entry : byFunction.entrySet()) {
			monitor.checkCancelled();
			monitor.incrementProgress(1);

			List<Claim> group = entry.getValue();
			group.sort((a, b) -> a.owner().name.compareToIgnoreCase(b.owner().name));

			Claim primary = group.get(0);
			for (Claim candidate : group) {
				boolean ancestorOfAll = true;
				for (Claim other : group) {
					if (other.owner() != candidate.owner()
						&& !other.owner().allAncestors.contains(candidate.owner())) {
						ancestorOfAll = false;
						break;
					}
				}
				if (ancestorOfAll) {
					primary = candidate;
					break;
				}
			}

			if (!rename(primary)) {
				continue;
			}
			count(primary);

			if (group.size() == 1) {
				continue;
			}

			shared++;
			var also = new StringBuilder("Shared by identical COMDAT folding with:");
			for (Claim other : group) {
				if (other == primary || other.owner() == primary.owner()) {
					continue;
				}
				also.append("\n    ").append(other.owner().name).append("::")
					.append(other.name());
				if (other.owner().namespace != null) {
					symbols.createLabel(entry.getKey().getEntryPoint(), other.name(),
						other.owner().namespace, SourceType.ANALYSIS);
				}
				count(other);
			}
			setPlateComment(entry.getKey().getEntryPoint(),
				primary.owner().name + " " + primary.kind() + "\n" + also);
		}
	}

	private void count(Claim claim) {
		switch (claim.kind()) {
			case "constructor" -> constructors++;
			case "scalar deleting destructor" -> deleting++;
			default -> destructors++;
		}
	}

	/**
	 * Recognises MSVC's scalar deleting destructor: it takes a flags argument, tests
	 * bit 0 to decide whether the object should also be freed, and calls at least the
	 * real destructor. Classes without a virtual destructor have an ordinary method in
	 * slot 0, and this is what keeps those from being mislabelled.
	 */
	private boolean looksLikeDeletingDestructor(Function function) {
		Instruction instruction = getInstructionAt(function.getEntryPoint());
		boolean testsLowBit = false;
		boolean calls = false;
		for (int i = 0; i < 40 && instruction != null; i++) {
			if (!function.getBody().contains(instruction.getAddress())) {
				break;
			}
			String mnemonic = instruction.getMnemonicString().toLowerCase();
			if (mnemonic.equals("test") || mnemonic.equals("and")) {
				Scalar scalar = instruction.getScalar(1);
				if (scalar != null && scalar.getUnsignedValue() == 1L) {
					testsLowBit = true;
				}
			}
			else if (mnemonic.equals("call")) {
				calls = true;
			}
			instruction = instruction.getNext();
		}
		return testsLowBit && calls;
	}

	/// Applies the primary name. Returns false when the function was left as it was.
	private boolean rename(Claim claim) {
		Function function = claim.function();
		RttiClass owner = claim.owner();
		Symbol symbol = function.getSymbol();
		if (symbol != null && symbol.getSource() == SourceType.USER_DEFINED) {
			kept++;
			return false;
		}
		try {
			if (owner.namespace != null && function.getParentNamespace() != owner.namespace) {
				function.setParentNamespace(owner.namespace);
			}
			function.setName(claim.name(), SourceType.ANALYSIS);
			setPlateComment(function.getEntryPoint(), owner.name + " " + claim.kind());
			return true;
		}
		catch (Exception e) {
			println("  ! " + owner.name + " " + claim.kind() + " at "
				+ function.getEntryPoint() + ": " + e.getMessage());
			return false;
		}
	}

	private GhidraClass ensureClass(String qualified) throws Exception {
		SymbolTable symbols = currentProgram.getSymbolTable();
		Namespace parent = currentProgram.getGlobalNamespace();
		String[] parts = qualified.split("::");
		for (int i = 0; i < parts.length; i++) {
			String part = sanitize(parts[i]);
			boolean last = i == parts.length - 1;
			Namespace existing = symbols.getNamespace(part, parent);
			if (existing != null) {
				if (last && !(existing instanceof GhidraClass)) {
					return null;
				}
				parent = existing;
				continue;
			}
			parent = last ? symbols.createClass(parent, part, SourceType.ANALYSIS)
				: symbols.createNameSpace(parent, part, SourceType.ANALYSIS);
		}
		return parent instanceof GhidraClass found ? found : null;
	}

	// =========================================================================
	// RTTI parsing, identical in behaviour to ReconstructClassesFromRtti.
	// =========================================================================

	private void loadBlocks() throws Exception {
		for (MemoryBlock b : currentProgram.getMemory().getBlocks()) {
			if (!b.isInitialized() || b.isVolatile()) {
				continue;
			}
			long size = b.getSize();
			if (size <= 0 || size > Integer.MAX_VALUE) {
				continue;
			}
			Blk blk = new Blk();
			blk.start = b.getStart().getOffset();
			blk.end = blk.start + size;
			blk.data = new byte[(int) size];
			b.getBytes(b.getStart(), blk.data);
			blk.exec = b.isExecute();
			blocks.add(blk);
			if (!blk.exec) {
				dataBlocks.add(blk);
			}
		}
	}

	private Blk blockOf(long va) {
		for (int i = 0; i < blocks.size(); i++) {
			Blk b = blocks.get(i);
			if (va >= b.start && va < b.end) {
				return b;
			}
		}
		return null;
	}

	private boolean inImage(long va) {
		return blockOf(va) != null;
	}

	private boolean inExec(long va) {
		Blk b = blockOf(va);
		return b != null && b.exec;
	}

	private int readInt(long va) {
		Blk b = blockOf(va);
		if (b == null || va + 4 > b.end) {
			return 0;
		}
		return readInt(b, (int) (va - b.start));
	}

	private static int readInt(Blk b, int off) {
		return (b.data[off] & 0xFF) | ((b.data[off + 1] & 0xFF) << 8)
			| ((b.data[off + 2] & 0xFF) << 16) | ((b.data[off + 3] & 0xFF) << 24);
	}

	private static long u32(int v) {
		return v & 0xFFFFFFFFL;
	}

	private Set<Long> findTypeDescriptors() throws Exception {
		Set<Long> found = new HashSet<>();
		for (Blk b : dataBlocks) {
			monitor.checkCancelled();
			byte[] d = b.data;
			for (int i = 0; i + 4 < d.length; i++) {
				if (d[i] != '.' || d[i + 1] != '?' || d[i + 2] != 'A') {
					continue;
				}
				if (d[i + 3] != 'V' && d[i + 3] != 'U') {
					continue;
				}
				long td = b.start + i - 8;
				if (td >= b.start && inImage(u32(readInt(td)))) {
					found.add(td);
				}
			}
		}
		return found;
	}

	private String readMangledName(long td) {
		Blk b = blockOf(td + 8);
		if (b == null) {
			return null;
		}
		int off = (int) (td + 8 - b.start);
		var sb = new StringBuilder();
		while (off < b.data.length && b.data[off] != 0) {
			sb.append((char) (b.data[off] & 0xFF));
			off++;
			if (sb.length() > 4096) {
				return null;
			}
		}
		return sb.toString();
	}

	private List<Long> findCompleteObjectLocators(Set<Long> tds) throws Exception {
		var cols = new ArrayList<Long>();
		for (Blk b : dataBlocks) {
			monitor.checkCancelled();
			byte[] d = b.data;
			for (int off = 0; off + 20 <= d.length; off += 4) {
				if (readInt(b, off) != 0) {
					continue;
				}
				int objOffset = readInt(b, off + 4);
				int cdOffset = readInt(b, off + 8);
				if (objOffset < 0 || objOffset > 0x100000 || cdOffset < 0
					|| cdOffset > 0x10000) {
					continue;
				}
				if (!tds.contains(u32(readInt(b, off + 12)))) {
					continue;
				}
				if (!isPlausibleChd(u32(readInt(b, off + 16)))) {
					continue;
				}
				cols.add(b.start + off);
			}
		}
		return cols;
	}

	private boolean isPlausibleChd(long chd) {
		if (!inImage(chd) || readInt(chd) != 0) {
			return false;
		}
		int attrs = readInt(chd + 4);
		if (attrs < 0 || attrs > 7) {
			return false;
		}
		int numBases = readInt(chd + 8);
		if (numBases < 1 || numBases > 2000) {
			return false;
		}
		long bca = u32(readInt(chd + 12));
		return inImage(bca) && inImage(bca + 4L * numBases - 4);
	}

	private void buildClasses(List<Long> cols) {
		for (long col : cols) {
			intern(u32(readInt(col + 12)), u32(readInt(col + 16)));
		}
		for (RttiClass c : new ArrayList<>(classesByTd.values())) {
			if (c.chd == 0) {
				continue;
			}
			int numBases = readInt(c.chd + 8);
			long bca = u32(readInt(c.chd + 12));
			for (int i = 0; i < numBases; i++) {
				long bcd = u32(readInt(bca + 4L * i));
				if (!inImage(bcd)) {
					continue;
				}
				long td = u32(readInt(bcd));
				if (!classesByTd.containsKey(td) && inImage(td)) {
					intern(td, 0);
				}
			}
		}
	}

	private RttiClass intern(long td, long chd) {
		RttiClass existing = classesByTd.get(td);
		if (existing != null) {
			if (existing.chd == 0 && chd != 0) {
				existing.chd = chd;
			}
			return existing;
		}
		String mangled = readMangledName(td);
		if (mangled == null || mangled.isEmpty()) {
			return null;
		}
		RttiClass c = new RttiClass();
		c.mangled = mangled;
		c.name = demangleTypeName(mangled);
		c.chd = chd;
		classesByTd.put(td, c);
		return c;
	}

	private static String demangleTypeName(String mangled) {
		String s = mangled;
		if (s.startsWith(".?AV") || s.startsWith(".?AU")) {
			s = s.substring(4);
		}
		while (s.endsWith("@")) {
			s = s.substring(0, s.length() - 1);
		}
		if (s.isEmpty()) {
			return mangled;
		}
		var scopes = new ArrayList<>(Arrays.asList(s.split("@")));
		Collections.reverse(scopes);
		var clean = new ArrayList<String>(scopes.size());
		for (String scope : scopes) {
			clean.add(sanitize(scope));
		}
		return String.join("::", clean);
	}

	private void resolveHierarchy() {
		for (RttiClass c : classesByTd.values()) {
			if (c.chd == 0) {
				continue;
			}
			int numBases = readInt(c.chd + 8);
			long bca = u32(readInt(c.chd + 12));
			parseBaseRange(c, bca, 1, numBases - 1, numBases);
		}
		for (RttiClass c : classesByTd.values()) {
			collectAncestors(c, c, new HashSet<RttiClass>());
		}
	}

	private void parseBaseRange(RttiClass owner, long bca, int start, int count, int total) {
		int i = start;
		int end = start + count;
		while (i < end && i < total) {
			long bcd = u32(readInt(bca + 4L * i));
			if (!inImage(bcd)) {
				return;
			}
			long td = u32(readInt(bcd));
			int contained = readInt(bcd + 4);
			if (contained < 0 || contained > total) {
				return;
			}
			RttiClass base = classesByTd.get(td);
			if (base != null && base != owner) {
				boolean known = false;
				for (RttiClass existing : owner.directBases) {
					if (existing == base) {
						known = true;
						break;
					}
				}
				if (!known) {
					owner.directBases.add(base);
				}
				if (contained > 0) {
					parseBaseRange(base, bca, i + 1, contained, total);
				}
			}
			i += 1 + contained;
		}
	}

	private void collectAncestors(RttiClass target, RttiClass current, Set<RttiClass> seen) {
		if (!seen.add(current)) {
			return;
		}
		for (RttiClass base : current.directBases) {
			if (base != target) {
				target.allAncestors.add(base);
			}
			collectAncestors(target, base, seen);
		}
	}

	private void locateVftables(List<Long> cols) throws Exception {
		Set<Long> colSet = new HashSet<>(cols);
		var vftableToCol = new HashMap<Long, Long>();
		for (Blk b : dataBlocks) {
			monitor.checkCancelled();
			for (int off = 0; off + 4 <= b.data.length; off += 4) {
				long value = u32(readInt(b, off));
				if (colSet.contains(value)) {
					colPointerSites.add(b.start + off);
					vftableToCol.put(b.start + off + 4, value);
				}
			}
		}
		for (Map.Entry<Long, Long> e : vftableToCol.entrySet()) {
			long col = e.getValue();
			RttiClass owner = classesByTd.get(u32(readInt(col + 12)));
			if (owner == null) {
				continue;
			}
			long addr = e.getKey();
			int slots = 0;
			while (slots < MAX_VFTABLE_SLOTS) {
				if (colPointerSites.contains(addr)) {
					break;
				}
				if (!inExec(u32(readInt(addr)))) {
					break;
				}
				slots++;
				addr += 4;
			}
			if (slots > 0) {
				owner.vftables.put(readInt(col + 4), e.getKey());
			}
		}
	}

	private static String sanitize(String s) {
		var sb = new StringBuilder(s.length());
		for (char ch : s.toCharArray()) {
			sb.append(Character.isLetterOrDigit(ch) || ch == '_' ? ch : '_');
		}
		String out = sb.toString();
		return out.isEmpty() ? "anon" : out;
	}
}
