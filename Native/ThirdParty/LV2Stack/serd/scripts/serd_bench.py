#!/usr/bin/env python

# Copyright 2018-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

"""Benchmark RDF reading and writing commands."""

# pylint: disable=consider-using-f-string
# pylint: disable=consider-using-with
# pylint: disable=import-outside-toplevel
# pylint: disable=invalid-name
# pylint: disable=redefined-outer-name
# pylint: disable=too-many-locals
# pylint: disable=unspecified-encoding

import argparse
import csv
import itertools
import math
import os
import subprocess
import sys

import matplotlib


class WorkingDirectory:
    "Scoped context for changing working directory"

    def __init__(self, working_dir):
        self.original_dir = os.getcwd()
        self.working_dir = working_dir

    def __enter__(self):
        sys.stderr.write("Entering directory `%s'\n" % self.working_dir)
        os.chdir(self.working_dir)
        return self

    def __exit__(self, exc_type, value, traceback):
        sys.stderr.write("Leaving directory `%s'\n" % self.working_dir)
        os.chdir(self.original_dir)


def order_of_magnitude(values):
    "Return the order of magnitude to use for an axis with the given values"
    if len(values) <= 0:
        return 0

    # Calculate the "best" order of magnitude like ScalarFormatter does
    val = max(values)
    oom = math.floor(math.log10(max(1.0, val)))
    if -3 <= oom <= 3:
        return 0

    # Round down to a sensible (thousand, millions, billions, etc) order
    remainder = oom % 3
    oom = oom - remainder
    return oom


def filename(num):
    "Filename for a generated file with n statements"
    return "gen%d.ttl" % num


def gen(sp2b_dir, n_min, n_max, step):
    "Generate files with n_min ... n_max statements if they are not present"
    with WorkingDirectory(sp2b_dir) as directory:
        for n in range(n_min, n_max + step, step):
            out_path = os.path.join(
                directory.original_dir, "build", filename(n)
            )
            if not os.path.exists(out_path):
                subprocess.call(["./sp2b_gen", "-t", str(n), out_path])


def write_header(results, progs):
    "Write the header line for TSV output"
    results.write("n")
    for prog in progs:
        results.write("\t" + os.path.basename(prog.split()[0]))
    results.write("\n")


def parse_time(report):
    "Return user time and max RSS from a /usr/bin/time -v report"
    time = memory = None
    for line in report.split("\n"):
        after_colon = line.find(":") + 1
        if line.startswith("\tUser time"):
            time = float(line[after_colon:])
        elif line.startswith("\tMaximum resident set"):
            memory = int(float(line[after_colon:]) * 1024)

    return (time, memory)


def get_dashes():
    "Generator for plot line dash patterns"
    dash = 2.0
    space = dot = 0.75

    yield []  # Solid
    yield [dash, space]  # Dashed
    yield [dot, space]  # Dotted

    # Dash-dots, with increasing number of dots for each line
    for i in itertools.count(2):
        yield [dash, space] + [dot, space] * (i - 1)


def plot(in_file, out_filename, x_label, y_label, y_max=None):
    "Plot a TSV file as SVG"
    matplotlib.use("agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.size": 7})

    fig_height = 1.8
    dashes = get_dashes()
    markers = itertools.cycle(["o", "s", "v", "D", "*", "p", "P", "h", "X"])

    reader = csv.reader(in_file, delimiter="\t")
    header = next(reader)
    cols = list(zip(*list(reader)))

    # Create a figure with a grid
    plt.clf()
    fig = plt.figure(figsize=(fig_height * math.sqrt(2), fig_height))
    ax = fig.add_subplot(111)
    ax.set_xlabel(x_label)
    ax.set_ylabel(y_label)

    ax.grid(linewidth=0.25, linestyle=":", color="0", dashes=[0.2, 1.6])
    ax.tick_params(axis="both", width=0.75)

    x = list(map(float, cols[0]))
    actual_y_max = 0.0
    for i, y in enumerate(cols[1::]):
        y_floats = list(map(float, y))
        y_floats_max = max(y_floats)
        actual_y_max = max(actual_y_max, y_floats_max)
        ax.plot(
            x,
            y_floats,
            label=header[i + 1],
            marker=next(markers),
            dashes=next(dashes),
            markersize=3.0,
            linewidth=1.0,
        )

    # Set Y axis limits to go from zero to the maximum value with a small pad
    y_max = (1.025 * actual_y_max) if y_max is None else y_max
    ax.set_ylim([0.0, y_max])

    # Set axis magnitudes
    x_m = (order_of_magnitude(x),) * 2
    y_m = (order_of_magnitude([y_max]),) * 2
    ax.ticklabel_format(axis="x", style="sci", scilimits=x_m, useMathText=True)
    ax.ticklabel_format(axis="y", style="sci", scilimits=y_m, useMathText=True)

    # Save plot
    plt.legend(labelspacing=0.25)
    plt.savefig(out_filename, bbox_inches="tight", pad_inches=0.125)
    plt.close()
    sys.stderr.write("wrote {}\n".format(out_filename))


def run(progs, n_min, n_max, step):
    "Benchmark each program with n_min ... n_max statements"
    with WorkingDirectory("build"):
        results = {
            "time": open("serdi-time.txt", "w"),
            "throughput": open("serdi-throughput.txt", "w"),
            "memory": open("serdi-memory.txt", "w"),
        }

        # Write TSV header for all output files
        for name, f in results.items():
            write_header(f, progs)

        for n in range(n_min, n_max + step, step):
            # Add first column (n) to rows
            rows = {}
            for name, _ in results.items():
                rows[name] = [str(n)]

            # Run each program and fill rows with measurements
            for prog in progs:
                cmd = "/usr/bin/time -v " + prog + " " + filename(n)
                with open(filename(n) + ".out", "w") as out:
                    sys.stderr.write(cmd + "\n")
                    proc = subprocess.Popen(
                        cmd.split(),
                        encoding="utf-8",
                        stdout=out,
                        stderr=subprocess.PIPE,
                    )

                    time, memory = parse_time(proc.communicate()[1])
                    rows["time"] += ["%.07f" % time]
                    rows["throughput"] += ["%d" % (n / time)]
                    rows["memory"] += [str(memory)]

            # Write rows to output files
            for name, f in results.items():
                f.write("\t".join(rows[name]) + "\n")

        for name, f in results.items():
            tsv_filename = "serdi-%s.txt" % name
            sys.stderr.write("wrote %s\n" % tsv_filename)


def plot_results():
    "Plot all benchmark results"
    with WorkingDirectory("build"):
        plot(
            open("serdi-time.txt", "r"),
            "serdi-time.svg",
            "Statements",
            "Time (s)",
        )
        plot(
            open("serdi-throughput.txt", "r"),
            "serdi-throughput.svg",
            "Statements",
            "Statements / s",
        )
        plot(
            open("serdi-memory.txt", "r"),
            "serdi-memory.svg",
            "Statements",
            "Bytes",
        )


if __name__ == "__main__":
    ap = argparse.ArgumentParser(
        usage="%(prog)s [OPTION]... SP2B_DIR",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
example:
  %(prog)s --max 300000 \\
      --run 'rapper -i turtle -o turtle' \\
      --run 'riot --output=ttl' \\
      --run 'rdfpipe -i turtle -o turtle' /path/to/sp2b/src/
""",
    )

    ap.add_argument(
        "--max", type=int, default=3000000, help="maximum triple count"
    )
    ap.add_argument(
        "--run",
        type=str,
        action="append",
        default=[],
        help="additional command to run (input file is appended)",
    )
    ap.add_argument(
        "--no-generate", action="store_true", help="do not generate data"
    )
    ap.add_argument(
        "--no-execute", action="store_true", help="do not run benchmarks"
    )
    ap.add_argument(
        "--no-plot", action="store_true", help="do not plot benchmarks"
    )
    ap.add_argument("--steps", type=int, default=6, help="number of steps")

    ap.add_argument("sp2b_dir", help="path to sp2b test data generator")

    args = ap.parse_args(sys.argv[1:])

    progs = ["serdi -b -f -i turtle -o turtle"] + args.run
    min_n = int(args.max / args.steps)
    max_n = args.max
    step = min_n

    if not args.no_generate:
        gen(args.sp2b_dir, min_n, max_n, step)
    if not args.no_execute:
        run(progs, min_n, max_n, step)
    if not args.no_plot:
        plot_results()
