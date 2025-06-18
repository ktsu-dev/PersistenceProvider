---
status: draft
---

## v1.0.0-pre.0 (major)

- Initial implementation with four persistence providers:
  - **MemoryPersistenceProvider**: In-memory storage using ConcurrentDictionary
  - **FileSystemPersistenceProvider**: File system storage with atomic operations
  - **AppDataPersistenceProvider**: Application data directory storage using %APPDATA%
  - **TempPersistenceProvider**: Temporary storage with automatic cleanup
- Generic key support (string, Guid, int, etc.)
- Full async/await support with cancellation tokens
- Integration with ktsu.SerializationProvider and ktsu.FileSystemProvider
- Dependency injection ready with proper abstraction
- Comprehensive error handling with PersistenceProviderException
- Thread-safe operations across all providers
- Atomic file operations to prevent data corruption
- Utility methods for safe filename generation and key conversion
- Full test coverage with 17 test cases ([@matt-edmondson](https://github.com/matt-edmondson))
