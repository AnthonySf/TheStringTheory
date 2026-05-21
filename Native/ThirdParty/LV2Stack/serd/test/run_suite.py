#!/usr/bin/env python3

# Copyright 2022-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

"""Run a "simple" one-pass RDF-based test suite for serd."""

import argparse
import os
import shlex
import subprocess
import sys
import tempfile

import serd_test_util as util

NS_MF = "http://www.w3.org/2001/sw/DataAccess/tests/test-manifest#"
NS_RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#"
NS_RDFT = "http://www.w3.org/ns/rdftest#"

DEVNULL = subprocess.DEVNULL
PIPE = subprocess.PIPE

TEST_TYPES = [
    NS_RDFT + "TestNQuadsNegativeSyntax",
    NS_RDFT + "TestNQuadsPositiveSyntax",
    NS_RDFT + "TestNTriplesNegativeSyntax",
    NS_RDFT + "TestNTriplesPositiveSyntax",
    NS_RDFT + "TestTrigEval",
    NS_RDFT + "TestTrigNegativeEval",
    NS_RDFT + "TestTrigNegativeSyntax",
    NS_RDFT + "TestTrigPositiveSyntax",
    NS_RDFT + "TestTurtleEval",
    NS_RDFT + "TestTurtleNegativeEval",
    NS_RDFT + "TestTurtleNegativeSyntax",
    NS_RDFT + "TestTurtlePositiveSyntax",
]


def run_eval_test(base_uri, command, in_path, good_path, out_path):
    """Run a positive eval test and return whether the output matches."""

    syntax = util.syntax_from_path(out_path)
    command = command + ["-o", syntax, in_path, base_uri]

    with subprocess.Popen(command, encoding="utf-8", stdout=PIPE) as proc:
        out = list(proc.stdout)

    with open(good_path, "r", encoding="utf-8") as good:
        return util.lines_equal(list(good), out, good_path, out_path)


def run_positive_test(base_uri, command, in_path):
    """Run a positive syntax test and ensure no errors occur."""

    command = command + [in_path, base_uri]
    subprocess.check_call(command, encoding="utf-8", stdout=DEVNULL)
    return True


def run_negative_test(base_uri, command, in_path):
    """Run a negative syntax test and return whether the error was detected."""

    if not os.path.exists(in_path):
        raise RuntimeError("Input file missing: " + in_path)

    command = command + [in_path, base_uri]
    proc = subprocess.run(
        command, check=False, encoding="utf-8", stderr=PIPE, stdout=DEVNULL
    )

    if proc.returncode == 0:
        util.error("Unexpected successful return: " + in_path)
        return False

    if proc.returncode < 0:
        util.error("Command seems to have crashed: " + in_path)
        return False

    if len(proc.stderr) == 0:
        util.error("Command failed with no error output: " + in_path)
        return False

    return True


def run_entry(args, entry, command, out_dir, suite_dir):
    """Run a single test entry from the manifest."""

    in_path = util.file_path(suite_dir, entry[NS_MF + "action"][0])
    base = args.base_uri + os.path.basename(in_path)

    negative = "Negative" in entry[NS_RDF + "type"][0]
    if negative and not args.lax:
        return run_negative_test(base, command, in_path)

    if NS_MF + "result" not in entry:
        return run_positive_test(base, command, in_path)

    good_path = util.file_path(suite_dir, entry[NS_MF + "result"][0])
    if args.reverse:
        in_path, good_path = good_path, in_path

    out_path = os.path.join(out_dir, os.path.basename(good_path))
    return run_eval_test(base, command, in_path, good_path, out_path)


def run_suite(args, command, out_dir):
    """Run all tests in the manifest."""

    # Load manifest model
    top = os.path.dirname(args.manifest)
    model, instances = util.load_rdf(args.manifest, args.base_uri, command)

    # Run all the listed tests that have known types
    command = command + args.arg
    results = util.Results()
    for klass, instances in instances.items():
        check = klass in [NS_RDFT + "TestTrigEval", NS_RDFT + "TestTurtleEval"]
        if klass == NS_MF + "Manifest":
            continue

        if klass not in TEST_TYPES:
            raise RuntimeError("Unknown manifest entry type: " + klass)

        for uri in instances:
            try:
                entry = model[uri]
                if check and NS_MF + "result" not in entry:
                    raise RuntimeError("Eval test missing result: " + uri)

                # Run test and record result
                passed = run_entry(args, entry, command, out_dir, top)
                results.check(passed)

                # Write test report entry
                if args.report:
                    with open(args.report, "a", encoding="utf-8") as report:
                        text = util.earl_assertion(uri, passed, args.asserter)
                        report.write(text)

            except subprocess.CalledProcessError as exception:
                if exception.stderr is not None:
                    sys.stderr.write(exception.stderr)

                results.check(False, str(exception) + "\n")

    return util.print_result_summary(results)


def main():
    """Run the command line tool."""

    parser = argparse.ArgumentParser(
        usage="%(prog)s [OPTION]... MANIFEST BASE_URI -- [ARG]...",
        description=__doc__,
    )

    parser.add_argument("--asserter", help="asserter URI for test report")
    parser.add_argument("--lax", action="store_true", help="tolerate errors")
    parser.add_argument("--report", help="path to write result report to")
    parser.add_argument("--reverse", action="store_true", help="reverse test")
    parser.add_argument("--serdi", default="serdi", help="path to serdi")
    parser.add_argument("--wrapper", default="", help="executable wrapper")
    parser.add_argument("manifest", help="test suite manifest.ttl file")
    parser.add_argument("base_uri", help="base URI for tests")
    parser.add_argument("arg", nargs=argparse.REMAINDER, help="serdi argument")

    args = parser.parse_args(sys.argv[1:])
    command = shlex.split(args.wrapper) + [args.serdi]

    with tempfile.TemporaryDirectory() as temp:
        return run_suite(args, command, temp)


if __name__ == "__main__":
    sys.exit(main())
