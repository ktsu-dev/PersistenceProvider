# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial release of PersistenceProvider library
- `IPersistenceProvider<TKey>` interface for generic persistence operations
- `MemoryPersistenceProvider<TKey>` for in-memory storage with serialization
- `FileSystemPersistenceProvider<TKey>` for file system-based persistence
- `AppDataPersistenceProvider<TKey>` for AppData integration
- `TempPersistenceProvider<TKey>` for temporary directory storage
- `PersistenceProviderException` for error handling
- Full async/await support for all operations
- Generic key type support (string, Guid, int, etc.)
- Integration with SerializationProvider for object serialization
- Integration with FileSystemProvider for file operations
- Thread-safe implementations across all providers
- Comprehensive XML documentation
- Unit tests with mock serialization provider
- README with detailed usage examples

### Features
- Store, retrieve, and manage objects with generic keys
- Multiple storage backends (memory, file system, app data, temp)
- Dependency injection ready
- Atomic file operations for data integrity
- Cancellation token support throughout
- Provider-specific cleanup methods (TempPersistenceProvider.Dispose)

## [1.0.0] - TBD

### Release Notes
- First stable release
- Full compatibility with SerializationProvider and FileSystemProvider libraries
- Production-ready implementations for all four provider types
- Complete API documentation and usage examples 