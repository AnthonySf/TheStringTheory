#!/usr/bin/env python3

# Copyright 2022-2025 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

"""Utilities for data-driven tests."""

# pylint: disable=consider-using-f-string
# pylint: disable=invalid-name

import argparse
import datetime
import difflib
import os
import re
import shlex
import subprocess
import sys
import urllib.parse


class Results:
    """Counts of test executions and failures."""

    def __init__(self):
        self.n_tests = 0
        self.n_failures = 0

    def test_passed(self):
        """Record a successful test."""
        self.n_tests += 1

    def test_failed(self):
        """Record a failed test."""
        self.n_tests += 1
        self.n_failures += 1

    def check(self, condition, message=None):
        """Check a test condition and update counts accordingly."""
        if not condition:
            self.test_failed()
            if message is not None:
                error(message)
        else:
            self.test_passed()


def error(message):
    """Log an error message to stderr."""

    sys.stderr.write("error: ")
    sys.stderr.write(message)
    sys.stderr.write("\n")


def wrapper_args(description, with_input=False):
    """Return the command line arguments for a wrapped test."""

    parser = argparse.ArgumentParser(description)
    parser.add_argument("--serdi", default="./serdi", help="serdi executable")
    parser.add_argument("--wrapper", default="", help="executable wrapper")
    if with_input:
        parser.add_argument("input", help="input file")

    return parser.parse_args(sys.argv[1:])


def command_output(wrapper, command, stdin=None):
    """Run a command and check that stdout matches the expected output."""

    proc = subprocess.run(
        shlex.split(wrapper) + command,
        capture_output=True,
        check=True,
        encoding="utf-8",
        input=stdin,
    )

    assert wrapper or not proc.stderr
    return proc.stdout


def print_result_summary(results):
    """Print test result summary to stdout or stderr as appropriate."""

    if results.n_tests <= 0:
        error("No tests found")
        return -1

    failed, total = (results.n_failures, results.n_tests)
    if failed == 0:
        sys.stdout.write("All {} tests passed\n".format(total))
    else:
        error("{}/{} tests failed".format(failed, total))

    return failed


def uri_path(uri):
    """Return the path component of a URI."""

    path = urllib.parse.urlparse(uri).path
    drive = os.path.splitdrive(path[1:])[0]
    return path if not drive else path[1:]


def file_path(suite_dir, uri):
    """Return a relative path to a file in a test suite."""

    return os.path.relpath(os.path.join(suite_dir, os.path.basename(uri)))


def syntax_from_path(path):
    """Return the serd syntax name corresponding to a file path."""

    extensions = {
        ".ttl": "turtle",
        ".nt": "ntriples",
        ".trig": "trig",
        ".nq": "nquads",
    }

    return extensions[os.path.splitext(path)[1]]


def earl_assertion(test, passed, asserter):
    """Return a Turtle description of an assertion for the test report."""

    asserter_str = ""
    if asserter is not None:
        asserter_str = "\n\tearl:assertedBy <%s> ;" % asserter

    return """
[]
\ta earl:Assertion ;%s
\tearl:subject <http://drobilla.net/sw/serd> ;
\tearl:test <%s> ;
\tearl:result [
\t\ta earl:TestResult ;
\t\tearl:outcome %s ;
\t\tdc:date "%s"^^xsd:dateTime
\t] .
""" % (
        asserter_str,
        test,
        "earl:passed" if passed else "earl:failed",
        datetime.datetime.now().replace(microsecond=0).isoformat(),
    )


def load_rdf(filename, base_uri, command_prefix):
    """Load an RDF file as dictionaries via serdi (only supports URIs)."""

    rdf_type = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type"
    model = {}
    instances = {}

    cmd = command_prefix + [filename, base_uri]
    proc = subprocess.run(
        cmd,
        encoding="utf-8",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    )
    for line in proc.stdout.splitlines():
        matches = re.match(r"<([^ ]*)> <([^ ]*)> <([^ ]*)> \.", line)
        if matches:
            s, p, o = (matches.group(1), matches.group(2), matches.group(3))
            if s not in model:
                model[s] = {p: [o]}
            elif p not in model[s]:
                model[s][p] = [o]
            else:
                model[s][p].append(o)

            if p == rdf_type:
                if o not in instances:
                    instances[o] = set([s])
                else:
                    instances[o].update([s])

    return model, instances


def lines_equal(from_lines, to_lines, from_filename, to_filename):
    """Return true if from_lines equals to_lines, or print a diff."""

    same = True
    for line in difflib.unified_diff(
        from_lines,
        to_lines,
        fromfile=os.path.abspath(from_filename),
        tofile=os.path.abspath(to_filename),
    ):
        sys.stderr.write(line)
        same = False

    return same
