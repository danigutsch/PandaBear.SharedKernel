# PandaBear.SharedKernel.DispositionHold.Abstractions

Jurisdiction-neutral contracts for holds that block deletion or anonymization.

A hold names a scope (an opaque kind and key), a neutral basis, an opaque purpose code, an opaque reference to the
record that authorizes it, the actors who placed and released it, an effective date, a review date and an optional
expiry. Every text field accepts only ASCII letters, digits and `. _ : -`, so names, e-mail addresses and free text
cannot be stored by accident.

The package owns hold state and the `IDispositionHoldGuard` contract. It does not own retention periods, legal bases,
jurisdiction rules, authorization or user interfaces; the host decides those. A purge worker calls the guard in the
same transaction as the disposition and refuses to proceed on a held decision or on
`DispositionHoldStateUnknownException`.

Provider modules, such as `PandaBear.SharedKernel.DispositionHold.EntityFrameworkCore`, supply storage and the
atomic guard.
