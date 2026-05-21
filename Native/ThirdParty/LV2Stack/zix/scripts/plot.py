#!/usr/bin/env python3

# Copyright 2011-2023 David Robillard <d@drobilla.net>
# SPDX-License-Identifier: ISC

"""
Plot a benchmark result.
"""

import math
import os
import sys

import matplotlib

from matplotlib import pyplot

matplotlib.rc("text", **{"usetex": True})

matplotlib.rc(
    "font",
    **{
        "family": "serif",
        "serif": "Times",
        "sans-serif": "Helvetica",
        "monospace": "Courier",
    },
)

pyplot.subplots_adjust(wspace=0.2, hspace=0.2)


class SensibleScalarFormatter(matplotlib.ticker.ScalarFormatter):
    "ScalarFormatter which rounds order of magnitude to a multiple of 3"

    def __init__(self):
        matplotlib.ticker.ScalarFormatter.__init__(self)
        self.set_powerlimits([-6, 6])
        self.set_scientific(True)

    def _set_order_of_magnitude(self):
        # Calculate "best" order in the usual way
        super()._set_order_of_magnitude()

        # Round down to sensible (millions, billions, etc) order
        self.orderOfMagnitude = self.orderOfMagnitude - (
            self.orderOfMagnitude % 3
        )

        self.set_scientific(True)


if __name__ == "__main__":
    file_prefix = os.path.commonprefix(sys.argv[1:])
    N_PLOTS = len(sys.argv) - 2
    for i in range(N_PLOTS):
        filename = sys.argv[i + 2]

        with open(filename, "r", encoding="utf-8") as in_file:
            ax = pyplot.subplot(
                math.ceil(math.sqrt(N_PLOTS)),
                math.ceil(math.sqrt(N_PLOTS)),
                i + 1,
            )

            ax.xaxis.set_major_formatter(SensibleScalarFormatter())
            ax.yaxis.set_major_formatter(SensibleScalarFormatter())
            for a in ["x", "y"]:
                ax.grid(
                    which="major",
                    axis=a,
                    zorder=1,
                    linewidth=0.5,
                    linestyle=":",
                    color="0",
                    dashes=[0.5, 8.0],
                )

            header = in_file.readline()
            columns = header[1:].split()

            pyplot.xlabel("Elements")
            pyplot.ylabel("Time (s)")

            times = [[] for i in range(len(columns))]
            for line in in_file:
                if line[0] == "#":
                    continue

                fields = line.split()
                for index, field in enumerate(fields):
                    times[index].append([float(field)])

        for t in range(len(times) - 1):
            matplotlib.pyplot.plot(
                times[0], times[t + 1], "-o", label=columns[t + 1]
            )

        pyplot.legend(
            loc="upper left",
            handletextpad=0.15,
            borderpad=0.20,
            borderaxespad=0,
            labelspacing=0.10,
            columnspacing=0,
            framealpha=0.90,
        )

        file_prefix_len = len(file_prefix)
        pyplot.title(os.path.splitext(filename[file_prefix_len:])[0].title())

    print(f"Writing {sys.argv[1]}")
    matplotlib.pyplot.tight_layout()
    matplotlib.pyplot.savefig(sys.argv[1])
