// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider;

using ktsu.FileSystemProvider;
using ktsu.SerializationProvider;

/// <summary>
/// A temporary directory-based persistence provider that stores objects in the system temporary directory.
/// Objects are typically cleaned up by the system but may persist between application sessions.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="TempPersistenceProvider{TKey}"/> class.
/// </remarks>
/// <param name="fileSystemProvider">The file system provider to use for file operations.</param>
/// <param name="serializationProvider">The serialization provider to use for object serialization.</param>
/// <param name="applicationName">Optional application name to create a subdirectory in temp folder.</param>
public sealed class TempPersistenceProvider<TKey>(
	IFileSystemProvider fileSystemProvider,
	ISerializationProvider serializationProvider,
	string? applicationName = null) : IPersistenceProvider<TKey>, IDisposable
	where TKey : notnull
{
	private readonly IFileSystemProvider _fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));
	private readonly ISerializationProvider _serializationProvider = serializationProvider ?? throw new ArgumentNullException(nameof(serializationProvider));
	private readonly string _tempDirectory = CreateTempDirectory(fileSystemProvider, applicationName);

	/// <inheritdoc/>
	public string ProviderName => "Temp";

	/// <inheritdoc/>
	public bool IsPersistent => false; // Temp files can be cleaned up by the system

	/// <inheritdoc/>
	public async Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (obj is null)
		{
			await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);
			string serializedData = await _serializationProvider.SerializeAsync(obj, cancellationToken).ConfigureAwait(false);

			// Ensure directory exists
			string? directory = _fileSystemProvider.Current.Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory))
			{
				_fileSystemProvider.Current.Directory.CreateDirectory(directory);
			}

			// Write to temporary file first, then move for atomic operation
			string tempFilePath = filePath + ".tmp";
			await _fileSystemProvider.Current.File.WriteAllTextAsync(tempFilePath, serializedData, cancellationToken).ConfigureAwait(false);

			// Atomic move
			if (_fileSystemProvider.Current.File.Exists(filePath))
			{
				_fileSystemProvider.Current.File.Delete(filePath);
			}
			_fileSystemProvider.Current.File.Move(tempFilePath, filePath);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to store object with key '{key}' to temp directory", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);

			if (!_fileSystemProvider.Current.File.Exists(filePath))
			{
				return default;
			}

			string serializedData = await _fileSystemProvider.Current.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

			if (string.IsNullOrEmpty(serializedData))
			{
				return default;
			}

			return await _serializationProvider.DeserializeAsync<T>(serializedData, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to retrieve object with key '{key}' from temp directory", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<T> RetrieveOrCreateAsync<T>(TKey key, CancellationToken cancellationToken = default) where T : new()
	{
		T? obj = await RetrieveAsync<T>(key, cancellationToken).ConfigureAwait(false);
		return obj ?? new T();
	}

	/// <inheritdoc/>
	public Task<bool> ExistsAsync(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		string filePath = GetFilePath(key);
		bool exists = _fileSystemProvider.Current.File.Exists(filePath);
		return Task.FromResult(exists);
	}

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string filePath = GetFilePath(key);

			if (!_fileSystemProvider.Current.File.Exists(filePath))
			{
				return Task.FromResult(false);
			}

			_fileSystemProvider.Current.File.Delete(filePath);
			return Task.FromResult(true);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to remove object with key '{key}' from temp directory", ex);
		}
	}

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_fileSystemProvider.Current.Directory.Exists(_tempDirectory))
			{
				return Task.FromResult(Enumerable.Empty<TKey>());
			}

			string[] files = _fileSystemProvider.Current.Directory.GetFiles(_tempDirectory, "*.json", SearchOption.TopDirectoryOnly);
			List<TKey> keys = [.. files
				.Select(f => _fileSystemProvider.Current.Path.GetFileNameWithoutExtension(f))
				.Where(name => !string.IsNullOrEmpty(name))
				.Select(name => PersistenceProviderUtilities.ConvertToKey<TKey>(name!))
				.Where(key => key is not null)
				.Cast<TKey>()];

			return Task.FromResult<IEnumerable<TKey>>(keys);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException("Failed to retrieve all keys from temp directory", ex);
		}
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_fileSystemProvider.Current.Directory.Exists(_tempDirectory))
			{
				return Task.CompletedTask;
			}

			string[] files = _fileSystemProvider.Current.Directory.GetFiles(_tempDirectory, "*.json", SearchOption.TopDirectoryOnly);
			foreach (string file in files)
			{
				_fileSystemProvider.Current.File.Delete(file);
			}

			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException("Failed to clear all objects from temp directory", ex);
		}
	}

	/// <summary>
	/// Releases resources. Does not automatically clean up the temporary directory.
	/// Use <see cref="CleanupDirectory"/> to explicitly remove the temporary directory if needed.
	/// </summary>
	public void Dispose() => GC.SuppressFinalize(this);

	/// <summary>
	/// Explicitly cleans up the temporary directory and all its contents.
	/// This is separate from Dispose() to give consumers control over directory cleanup.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intentionally ignoring cleanup errors")]
	public void CleanupDirectory()
	{
		try
		{
			if (_fileSystemProvider.Current.Directory.Exists(_tempDirectory))
			{
				_fileSystemProvider.Current.Directory.Delete(_tempDirectory, recursive: true);
			}
		}
		catch (Exception)
		{
			// Ignore cleanup errors - temp directories may be cleaned up by the system
			// Intentionally catching all exceptions during cleanup
		}
	}

	private string GetFilePath(TKey key)
	{
		string fileName = PersistenceProviderUtilities.GetSafeFileName(key.ToString()!) + ".json";
		return _fileSystemProvider.Current.Path.Combine(_tempDirectory, fileName);
	}

	private static string CreateTempDirectory(IFileSystemProvider fileSystemProvider, string? applicationName)
	{
		string tempPath = fileSystemProvider.Current.Path.GetTempPath();
		string directoryName = applicationName ?? "PersistenceProvider";
		string tempDirectory = fileSystemProvider.Current.Path.Combine(tempPath, directoryName, Guid.NewGuid().ToString("N")[..8]);

		fileSystemProvider.Current.Directory.CreateDirectory(tempDirectory);
		return tempDirectory;
	}
}
