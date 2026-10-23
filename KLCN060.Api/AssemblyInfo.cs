using System.Runtime.CompilerServices;

// [Khoi 5 - Unit Testing, 24/09/2026] Cho phep KLCN060.Api.Tests truy cap cac ham "internal static"
// thuan tuy (validate/format) trong Services - chi de kiem thu, khong doi hanh vi runtime.
[assembly: InternalsVisibleTo("KLCN060.Api.Tests")]
