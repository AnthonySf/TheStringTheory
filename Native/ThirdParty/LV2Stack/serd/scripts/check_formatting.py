#!/usr/bin/env python3

# Copyright 2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

"""Run a file through a formatter and error if the output differs."""

import argparse
import difflib
import os
import shlex
import subprocess
import sys

DEVNULL = subprocess.DEVNULL
PIPE = subprocess.PIPE


def run_formatter(command, good_path):
    """Run the formatter and compare the output."""

    with subprocess.Popen(
        command, encoding="utf-8", stderr=DEVNULL, stdout=PIPE
    ) as proc:
        out = list(proc.stdout)

    with open(good_path, "r", encoding="utf-8") as good:
        good_lines = list(good)

    same = True
    for line in difflib.unified_diff(good_lines, out, fromfile=good_path):
        sys.stderr.write(line)
        same = False

    return same


def main():
    """Run the command line tool."""

    parser = argparse.ArgumentParser(
        usage="%(prog)s [OPTION]... INPUT TOOL [ARG]...", description=__doc__
    )

    parser.add_argument("--wrapper", default="", help="executable wrapper")
    parser.add_argument("input", help="file to check")
    parser.add_argument("tool", help="formatter executable")
    parser.add_argument("arg", nargs=argparse.REMAINDER, help="tool argument")

    args = parser.parse_args(sys.argv[1:])
    path = os.path.normpath(args.input)
    command = shlex.split(args.wrapper) + [args.tool] + args.arg + [path]

    return 0 if run_formatter(command, args.input) else 1


if __name__ == "__main__":
    sys.exit(main())
