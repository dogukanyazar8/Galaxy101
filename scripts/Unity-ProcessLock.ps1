function Enter-GalaxyUnityProcess {
    # Shared by builds, native tests and captures. A nested capture build owns the same mutex recursively.
    $operationMutex = [Threading.Mutex]::new($false, 'Local\Galaxy101.UnityProcess')
    try {
        try { $acquired = $operationMutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Another Unity operation is running. Wait for it to finish; no process was stopped.' }
        if (@(Get-Process -Name Unity, Galaxy101 -ErrorAction SilentlyContinue).Count -gt 0) {
            $operationMutex.ReleaseMutex()
            throw 'A Unity editor or Galaxy101 player is already running. Close it before continuing; no process was stopped.'
        }
        return $operationMutex
    } catch {
        $operationMutex.Dispose()
        throw
    }
}
