Bad Test Suite
==============

This suite contains tests that are "bad" in the sense that they are negative
tests which contain errors the implementation must detect.  These tests have no
results to compare, since parsing is expected to fail.  The implementation may
emit some triples before failing, but this output is ignored.
