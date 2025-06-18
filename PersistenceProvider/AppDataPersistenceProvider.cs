// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider;

using ktsu.FileSystemProvider;
using ktsu.SerializationProvider;

/// <summary>
/// An AppData-based persistence provider that stores objects in the application's data directory.
/// Objects persist in the application's data directory using standard file system operations.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
public sealed class AppDataPersistenceProvider<TKey> : IPersistenceProvider<TKey>
	where TKey : notnull
{
	private readonly IFileSystemProvider _fileSystemProvider;
	private readonly ISerializationProvider _serializationProvider;
	private readonly string _baseDirectory;

	/// <summary>
	/// Initializes a new instance of the <see cref="AppDataPersistenceProvider{TKey}"/> class.
	/// </summary>
	/// <param name="fileSystemProvider">The file system provider to use for file operations.</param>
	/// <param name="serializationProvider">The serialization provider to use for object serialization.</param>
	/// <param name="applicationName">The application name to use for the data directory.</param>
	/// <param name="subdirectory">Optional subdirectory within the AppData folder to store objects.</param>
	public AppDataPersistenceProvider(
		IFileSystemProvider fileSystemProvider,
		ISerializationProvider serializationProvider,
		string applicationName,
		string? subdirectory = null)
	{
		_fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));
		_serializationProvider = serializationProvider ?? throw new ArgumentNullException(nameof(serializationProvider));
		_baseDirectory = GetAppDataDirectory(applicationName, subdirectory);
	}

	/// <inheritdoc/>
	public string ProviderName => "AppData";

	/// <inheritdoc/>
	public bool IsPersistent => true;

	/// <inheritdoc/>
	public async Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (obj is null)
			{
				await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
				return;
			}

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
			throw new PersistenceProviderException($"Failed to store object with key '{key}' to AppData", ex);
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
			throw new PersistenceProviderException($"Failed to retrieve object with key '{key}' from AppData", ex);
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

		try
		{
			string filePath = GetFilePath(key);
			bool exists = _fileSystemProvider.Current.File.Exists(filePath);
			return Task.FromResult(exists);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to check existence of object with key '{key}' in AppData", ex);
		}
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
			throw new PersistenceProviderException($"Failed to remove object with key '{key}' from AppData", ex);
		}
	}

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_fileSystemProvider.Current.Directory.Exists(_baseDirectory))
			{
				return Task.FromResult(Enumerable.Empty<TKey>());
			}

			string[] files = _fileSystemProvider.Current.Directory.GetFiles(_baseDirectory, "*.json", SearchOption.TopDirectoryOnly);
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
			throw new PersistenceProviderException("Failed to retrieve all keys from AppData", ex);
		}
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_fileSystemProvider.Current.Directory.Exists(_baseDirectory))
			{
				return Task.CompletedTask;
			}

			string[] files = _fileSystemProvider.Current.Directory.GetFiles(_baseDirectory, "*.json", SearchOption.TopDirectoryOnly);
			foreach (string file in files)
			{
				_fileSystemProvider.Current.File.Delete(file);
			}

			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException("Failed to clear all objects from AppData", ex);
		}
	}

	private string GetFilePath(TKey key)
	{
		string fileName = PersistenceProviderUtilities.GetSafeFileName(key.ToString()!) + ".json";
		return _fileSystemProvider.Current.Path.Combine(_baseDirectory, fileName);
	}

	private string GetAppDataDirectory(string applicationName, string? subdirectory)
	{
		if (string.IsNullOrWhiteSpace(applicationName))
		{
			throw new ArgumentException("Application name cannot be null or whitespace.", nameof(applicationName));
		}

		// Environment.GetFolderPath is a system API, not file I/O, so it's acceptable to use directly
		string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string basePath = _fileSystemProvider.Current.Path.Combine(appDataPath, applicationName);

		if (!string.IsNullOrWhiteSpace(subdirectory))
		{
			basePath = _fileSystemProvider.Current.Path.Combine(basePath, subdirectory);
		}

		return basePath;
	}
}
