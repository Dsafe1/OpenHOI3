"""Sets a host's and a client's dump of a parted game beside each other and prints the figures that differ.

Usage: python partdiff.py <logs folder> [hour]   - the newest hour that has both a host's and a client's file.
"""
import glob
import io
import os
import re
import sys


def load(path):
    figures = {}
    with io.open(path, encoding="utf-8") as lines:
        for line in lines:
            name, _, value = line.rstrip("\n").rpartition("\t")
            figures[name] = value
    return figures


def main():
    folder = sys.argv[1]
    hours = {}
    for path in glob.glob(os.path.join(folder, "openhoi3_parted_*.txt")):
        found = re.match(r"openhoi3_parted_(\d+)_(host|client)", os.path.basename(path))
        if found:
            hours.setdefault(found.group(1), {}).setdefault(found.group(2), []).append(path)
    wanted = sys.argv[2] if len(sys.argv) > 2 else None
    both = sorted(hour for hour, kinds in hours.items() if "host" in kinds and "client" in kinds)
    if wanted:
        both = [hour for hour in both if hour == wanted]
    if not both:
        print("no hour has both a host's and a client's file")
        return
    for hour in both:
        host = load(hours[hour]["host"][0])
        for path in hours[hour]["client"]:
            client = load(path)
            differing = sorted(name for name in set(host) | set(client) if host.get(name) != client.get(name))
            print("hour %s, %s: %d of %d figures differ" % (hour, os.path.basename(path), len(differing), len(host)))
            for name in differing[:60]:
                print("   %s   host %s   client %s" % (name, host.get(name, "-"), client.get(name, "-")))
            if len(differing) > 60:
                print("   ... and %d more" % (len(differing) - 60))


main()
