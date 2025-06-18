# PersistenceProvider

A generic persistence provider library that supports multiple storage backends for .NET applications. This library is designed to complement and integrate with SerializationProvider and FileSystemProvider libraries, providing a clean abstraction layer for data persistence with dependency injection support.

## Features

- **Multiple Storage Backends**: Memory, file system, application data, and temporary storage
- **Dependency Injection Ready**: Designed for use with DI containers
- **Async/Await Support**: All operations are asynchronous
- **Generic Key Support**: Use any type as a key (string, Guid, int, etc.)
- **Serialization Integration**: Works with any ISerializationProvider implementation
- **File System Integration**: Leverages IFileSystemProvider for file operations
- **Thread-Safe**: Concurrent operations are handled safely

## Installation

```bash
# Install from NuGet (when published)
dotnet add package ktsu.PersistenceProvider

# Or add project reference
dotnet add reference path/to/PersistenceProvider.csproj
```

## Quick Start

### Basic Usage with Memory Provider

```csharp
using ktsu.PersistenceProvider;
using SerializationProvider;

// Create a serialization provider (implementation not shown)
ISerializationProvider serializer = new JsonSerializationProvider();

// Create a memory-based persistence provider
IPersistenceProvider<string> provider = new MemoryPersistenceProvider<string>(serializer);

// Store an object
await provider.StoreAsync("user:123", new UserSettings 
{ 
    Theme = "Dark", 
    Language = "en-US" 
});

// Retrieve the object
var settings = await provider.RetrieveAsync<UserSettings>("user:123");

// Check if an object exists
bool exists = await provider.ExistsAsync("user:123");

// Remove an object
await provider.RemoveAsync("user:123");
```

### File System Provider

```csharp
using ktsu.PersistenceProvider;
using ktsu.FileSystemProvider;
using SerializationProvider;

// Create providers
IFileSystemProvider fileSystem = new FileSystemProvider();
ISerializationProvider serializer = new JsonSerializationProvider();

// Create file system-based persistence provider
IPersistenceProvider<string> provider = new FileSystemPersistenceProvider<string>(
    fileSystem, 
    serializer, 
    @"C:\MyApp\Data");

// Use same interface as memory provider
await provider.StoreAsync("config", new AppConfig { Version = "1.0" });
var config = await provider.RetrieveAsync<AppConfig>("config");
```

### AppData Provider

```csharp
using ktsu.PersistenceProvider;
using ktsu.AppData.Interfaces;
using ktsu.Semantics;

// Create AppData repository (implementation depends on your setup)
IAppDataRepository<PersistenceItem> repository = CreateAppDataRepository();

// Create AppData-based persistence provider
IPersistenceProvider<string> provider = new AppDataPersistenceProvider<string>(
    repository, 
    "MyApp".As<RelativeDirectoryPath>());

// Store application data
await provider.StoreAsync("preferences", new UserPreferences 
{ 
    AutoSave = true,
    CheckForUpdates = false 
});
```

### Temporary Storage Provider

```csharp
using ktsu.PersistenceProvider;
using ktsu.FileSystemProvider;
using SerializationProvider;

// Create temporary storage provider
IPersistenceProvider<Guid> provider = new TempPersistenceProvider<Guid>(
    fileSystemProvider, 
    serializationProvider, 
    "MyApp");

// Store temporary data
Guid sessionId = Guid.NewGuid();
await provider.StoreAsync(sessionId, new SessionData { StartTime = DateTime.UtcNow });

// Clean up on dispose
using var tempProvider = provider as TempPersistenceProvider<Guid>;
tempProvider?.Dispose(cleanupDirectory: true);
```

## Dependency Injection

Register persistence providers in your DI container:

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register dependencies
services.AddSingleton<ISerializationProvider, JsonSerializationProvider>();
services.AddSingleton<IFileSystemProvider, FileSystemProvider>();

// Register persistence providers
services.AddSingleton<IPersistenceProvider<string>>(provider =>
    new MemoryPersistenceProvider<string>(
        provider.GetRequiredService<ISerializationProvider>()));

services.AddSingleton<IPersistenceProvider<Guid>>(provider =>
    new FileSystemPersistenceProvider<Guid>(
        provider.GetRequiredService<IFileSystemProvider>(),
        provider.GetRequiredService<ISerializationProvider>(),
        @"C:\MyApp\Data"));

// Use in your services
services.AddTransient<IUserService, UserService>();
```

## Available Providers

### MemoryPersistenceProvider<TKey>
- **Storage**: In-memory dictionary
- **Persistence**: No (data lost on application exit)
- **Use Case**: Caching, temporary data, testing
- **Thread Safety**: Yes (ConcurrentDictionary)

### FileSystemPersistenceProvider<TKey>
- **Storage**: File system as JSON files
- **Persistence**: Yes (survives application restart)
- **Use Case**: Configuration files, user data, application state
- **Thread Safety**: Yes (atomic file operations)

### AppDataPersistenceProvider<TKey>
- **Storage**: Application data directory
- **Persistence**: Yes (uses existing AppData infrastructure)
- **Use Case**: User-specific application data
- **Thread Safety**: Depends on underlying AppData implementation

### TempPersistenceProvider<TKey>
- **Storage**: System temporary directory
- **Persistence**: Limited (may be cleaned up by system)
- **Use Case**: Temporary files, cache, session data
- **Thread Safety**: Yes (atomic file operations)

## Interface

```csharp
public interface IPersistenceProvider<TKey> where TKey : notnull
{
    string ProviderName { get; }
    bool IsPersistent { get; }
    
    Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default);
    Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default);
    Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken = default) where T : new();
    Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default);
    Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

## Error Handling

All providers throw `PersistenceProviderException` for operation failures:

```csharp
try 
{
    await provider.StoreAsync("key", data);
}
catch (PersistenceProviderException ex)
{
    // Handle persistence-specific errors
    Console.WriteLine($"Storage failed: {ex.Message}");
}
```

## Integration with Other Libraries

This library is designed to work with:

- **SerializationProvider**: For object serialization/deserialization
- **FileSystemProvider**: For file system operations with testing support
- **AppData**: For application data management
- **Any DI Container**: Microsoft.Extensions.DependencyInjection, Autofac, etc.

## Building

```bash
dotnet build PersistenceProvider.sln
```

## Testing

```bash
dotnet test PersistenceProvider.sln
```

## Contributing

This library follows the same patterns and conventions as other ktsu.dev libraries. Please ensure:

- All public APIs are documented with XML comments
- Code follows established patterns from SerializationProvider and FileSystemProvider
- Tests are included for new functionality
- Changes are backward compatible

## License

MIT License - see LICENSE file for details. 