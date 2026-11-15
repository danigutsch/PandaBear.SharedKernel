### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SKPRX001 | SharedKernel.Testing.CallProxy | Error | CallProxy can only wrap an interface
SKPRX002 | SharedKernel.Testing.CallProxy | Error | CallProxy needs an interface the test assembly can implement
SKPRX003 | SharedKernel.Testing.CallProxy | Error | CallProxy supports interface methods without ref, out or in parameters only
SKPRX004 | SharedKernel.Testing.CallProxy | Error | CallProxy typed hooks need C# 14
