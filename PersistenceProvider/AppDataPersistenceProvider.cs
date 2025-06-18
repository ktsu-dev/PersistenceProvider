// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider;

using ktsu.AppData.Interfaces;
using ktsu.Semantics;

/// <summary>
/// Internal class to wrap objects for AppData storage.
/// </summary>
public sealed class PersistenceItem
{
	/// <summary>
	/// Gets or sets the unique key that identifies the stored object.
	/// </summary>
	public string Key { get; set; } = string.Empty;
	
	/// <summary>
	/// Gets or sets the assembly-qualified type name of the stored object.
	/// </summary>
	public string TypeName { get; set; } = string.Empty;
	
	/// <summary>
	/// Gets or sets the actual data object being persisted.
	/// </summary>
	public object? Data { get; set; }
}

/// <summary>
/// An AppData-based persistence provider that stores objects using the existing AppData infrastructure.
/// Objects persist in the application's data directory.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="AppDataPersistenceProvider{TKey}"/> class.
/// </remarks>
/// <param name="repository">The AppData repository to use for storage operations.</param>
/// <param name="subdirectory">Optional subdirectory within the AppData folder to store objects.</param>
public sealed class AppDataPersistenceProvider<TKey>(
	IAppDataRepository<PersistenceItem> repository,
	RelativeDirectoryPath? subdirectory = null) : IPersistenceProvider<TKey>
	where TKey : notnull
{
	private readonly IAppDataRepository<PersistenceItem> _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly RelativeDirectoryPath? _subdirectory = subdirectory;

	/// <inheritdoc/>
	public string ProviderName => "AppData";

	/// <inheritdoc/>
	public bool IsPersistent => true;

	/// <inheritdoc/>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (obj is null)
			{
				return RemoveAsync(key, cancellationToken);
			}

			var item = new PersistenceItem
			{
				Key = key.ToString()!,
				TypeName = typeof(T).AssemblyQualifiedName!,
				Data = obj
			};

			FileName fileName = GetFileName(key);
			_repository.Save(item, _subdirectory, fileName);
			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to store object with key '{key}' to AppData", ex);
		}
	}

	/// <inheritdoc/>
	public Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			FileName fileName = GetFileName(key);
			string content = _repository.ReadText(_subdirectory, fileName);
			
			if (string.IsNullOrEmpty(content))
			{
				return Task.FromResult<T?>(default);
			}

			var item = _repository.LoadOrCreate(_subdirectory, fileName);
			
			if (item.Data is T typedData)
			{
				return Task.FromResult<T?>(typedData);
			}

			return Task.FromResult<T?>(default);
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
			FileName fileName = GetFileName(key);
			string content = _repository.ReadText(_subdirectory, fileName);
			return Task.FromResult(!string.IsNullOrEmpty(content));
		}
		catch
		{
			return Task.FromResult(false);
		}
	}

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			FileName fileName = GetFileName(key);
			
			// Check if file exists first
			string content = _repository.ReadText(_subdirectory, fileName);
			if (string.IsNullOrEmpty(content))
			{
				return Task.FromResult(false);
			}

			// Remove by writing empty content
			_repository.WriteText(string.Empty, _subdirectory, fileName);
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
			// This is a limitation of the current AppData infrastructure - 
			// it doesn't provide a way to enumerate all files in a directory.
			// For now, we return an empty collection.
			// In a real implementation, you might need to extend the AppData interfaces
			// to support directory enumeration.
			return Task.FromResult(Enumerable.Empty<TKey>());
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
			// This is a limitation of the current AppData infrastructure - 
			// it doesn't provide a way to clear all files in a directory.
			// For now, this is a no-op.
			// In a real implementation, you might need to extend the AppData interfaces
			// to support directory clearing.
			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException("Failed to clear all objects from AppData", ex);
		}
	}

	private FileName GetFileName(TKey key)
	{
		string keyString = key.ToString()!;
		string safeFileName = GetSafeFileName(keyString);
		return (safeFileName + ".json").As<FileName>();
	}

	private static string GetSafeFileName(string input)
	{
		var invalidChars = Path.GetInvalidFileNameChars();
		return string.Concat(input.Select(c => invalidChars.Contains(c) ? '_' : c));
	}
} 