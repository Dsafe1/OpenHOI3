"""Sets two machines' lists of the play checksum beside each other and prints what differs.

Each machine writes openhoi3_checksum.txt into its logs folder as the main menu works its play checksum out: a line for
the format, the engine's version and the rules, then one for every file that went in - its path, its length and the
first sixteen digits of its own SHA-256 - and the checksum last (docs/mechanics/checksum.md, section 8).

Usage: python checksumdiff.py <one machine's openhoi3_checksum.txt> <the other's>
"""
import io
import sys


def load(path):
    entries = {}
    with io.open(path, encoding="utf-8-sig") as lines:
        for line in lines:
            parts = line.rstrip("\r\n").split("\t")
            if parts[0]:
                entries[parts[0]] = tuple(parts[1:])
    return entries


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return
    mine, theirs = load(sys.argv[1]), load(sys.argv[2])
    only_mine = sorted(name for name in mine if name not in theirs)
    only_theirs = sorted(name for name in theirs if name not in mine)
    differing = sorted(name for name in mine if name in theirs and mine[name] != theirs[name])
    print("%d entries in the first, %d in the second" % (len(mine), len(theirs)))
    print("only in the first: %d" % len(only_mine))
    for name in only_mine[:80]:
        print("   %s   %s" % (name, " ".join(mine[name])))
    print("only in the second: %d" % len(only_theirs))
    for name in only_theirs[:80]:
        print("   %s   %s" % (name, " ".join(theirs[name])))
    print("in both and not the same: %d" % len(differing))
    for name in differing[:80]:
        print("   %s   first %s   second %s" % (name, " ".join(mine[name]), " ".join(theirs[name])))


main()
