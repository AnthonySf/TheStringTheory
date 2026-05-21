# AudioDSPTools
A library of basic DSP things.

## Sharp edges
This library uses [Eigen](http://eigen.tuxfamily.org) to do some linear algebra. If they are stored as members of objects, there is a risk with certain compilers and compiler optimizations that the memory associated with eigen objects is not aligned properly. This can be worked around by providing two preprocessor macros: `EIGEN_MAX_ALIGN_BYTES 0` and `EIGEN_DONT_VECTORIZE`, though this will probably harm performance. See [Structs Having Eigen Members](http://eigen.tuxfamily.org/dox-3.2/group__TopicStructHavingEigenMembers.html) and [NeuralAmpModelerCore Issue 67](https://github.com/sdatkinson/NeuralAmpModelerCore/issues/67) for more information.
