namespace SharedKernel.Idempotency;

/// <summary>
/// Identifies one idempotent operation by scope and key.
/// </summary>
/// <param name="Scope">The operation scope.</param>
/// <param name="Key">The operation key.</param>
public readonly record struct IdempotencyOperation(IdempotencyScope Scope, IdempotencyKey Key);
