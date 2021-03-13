# netcircllhist

This is a C# .NET implementation of the OpenHistogram [libcircllhist](https://github.com/openhistogram/libcircllhist) library.

It provides the `Histogram` class from a `Circonus.circllhist` namespace in an
asset called `netcircllhist.dll`.


## Usage

```
using Circonus.circllhist;

...

Histogram hist = new Histogram();
Histogram cumulative = new Histogram();

hist.Insert(1.34, 1); // Insert one sample at 1.34 bin: [1.3,1.4)
hist.Insert(934, -9, 2); // Insert two samples at 934e-9 bin: [9.3e-7,9.4e-7)

var serialized = hist.ToBase64String();

Histogram fromSerialized = newHistogram(serialized);

cumulative.Merge(hist); // accumulate hist into cumulative
