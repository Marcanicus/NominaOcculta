using System;

namespace NominaOcculta;

[Flags]
internal enum Sex {
    Male = 1 << 1,
    Female = 1 << 2,
}
