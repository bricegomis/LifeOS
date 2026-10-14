export class SupersededNativeOperation extends Error {
  constructor() { super('Identité modifiée : opération native annulée') }
}

// Purge invalidates immediately, then waits for any already admitted native write.
export class NativeStateCoordinator {
  private generation = 0
  private tail: Promise<unknown> = Promise.resolve()
  private enabled = true

  token(): number { return this.generation }
  isCurrent(token: number): boolean { return token === this.generation }
  invalidate(): void { this.generation++ }
  pause(): void { this.invalidate(); this.enabled = false }
  resume(): void { this.enabled = true }

  run<T>(token: number, operation: () => Promise<T>): Promise<T> {
    return this.enqueue(async () => {
      if (!this.enabled || !this.isCurrent(token)) throw new SupersededNativeOperation()
      const result = await operation()
      if (!this.enabled || !this.isCurrent(token)) throw new SupersededNativeOperation()
      return result
    })
  }

  cleanup(operation: () => Promise<void>): Promise<void> {
    return this.enqueue(operation)
  }

  private enqueue<T>(operation: () => Promise<T>): Promise<T> {
    const next = this.tail.then(async () => {
      return operation()
    })
    this.tail = next.then(() => undefined, () => undefined)
    return next
  }
}

export const nativeStateCoordinator = new NativeStateCoordinator()
