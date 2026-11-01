### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
SKIO001 | IO | Warning | Request or upload streams should not be buffered into memory without an explicit length bound
SKIO002 | IO | Warning | Hand-rolled stream copy loops into a MemoryStream should use SeekableContent or enforce a length bound
SKIO003 | IO | Disabled | Upload and raw-body endpoints should set a request size limit; enable warning severity where the host does not enforce one
