# Map Visualizer
There are two implementations of Interpolator:
- ManhattanDistInterpolator (fast)
- EuclideanDistInterpolator (slow, consumes more memory, but probably more accurate)

It uses ManhattanDistInterpolator by default.
Change variable `interpolator` in class `ImageBuilder` to `new EuclideanDistInterpolator()` to use another interpolator.