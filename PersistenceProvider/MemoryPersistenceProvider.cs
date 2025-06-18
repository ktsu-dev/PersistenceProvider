// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider;

using System.Collections.Concurrent;
using SerializationProvider;

/// <summary>
/// A memory-based persistence provider that stores objects in memory using serialization.
/// Objects are lost when the application terminates.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored objects.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="MemoryPersistenceProvider{TKey}"/> class.
/// </remarks>
/// <param name="serializationProvider">The serialization provider to use for object serialization.</param>
public sealed class MemoryPersistenceProvider<TKey>(ISerializationProvider serializationProvider) : IPersistenceProvider<TKey> 
	where TKey : notnull
{
	private readonly ISerializationProvider _serializationProvider = serializationProvider ?? throw new ArgumentNullException(nameof(serializationProvider));
	private readonly ConcurrentDictionary<TKey, string> _storage = new();

	/// <inheritdoc/>
	public string ProviderName => "Memory";

	/// <inheritdoc/>
	public bool IsPersistent => false;

	/// <inheritdoc/>
	public Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (obj is null)
		{
			return RemoveAsync(key, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string serializedData = _serializationProvider.Serialize(obj);
			_storage.AddOrUpdate(key, serializedData, (_, _) => serializedData);
			return Task.CompletedTask;
		}
		catch (Exception ex)
		{
			return Task.FromException(new PersistenceProviderException($"Failed to store object with key '{key}'", ex));
		}
	}

	/// <inheritdoc/>
	public Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_storage.TryGetValue(key, out string? serializedData) || serializedData is null)
			{
				return Task.FromResult<T?>(default);
			}

			T? obj = _serializationProvider.Deserialize<T>(serializedData);
			return Task.FromResult<T?>(obj);
		}
		catch (Exception ex)
		{
			return Task.FromException<T?>(new PersistenceProviderException($"Failed to retrieve object with key '{key}'", ex));
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

		bool exists = _storage.ContainsKey(key);
		return Task.FromResult(exists);
	}

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		bool removed = _storage.TryRemove(key, out _);
		return Task.FromResult(removed);
	}

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IEnumerable<TKey> keys = _storage.Keys.ToList(); // Create a snapshot to avoid concurrent modification issues
		return Task.FromResult(keys);
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		_storage.Clear();
		return Task.CompletedTask;
	}
} 