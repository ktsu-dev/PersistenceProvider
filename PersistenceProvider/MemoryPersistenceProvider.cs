// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider;

using System.Collections.Concurrent;
using ktsu.SerializationProvider;

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
	private readonly ISerializationProvider _serializationProvider = Ensure.NotNull(serializationProvider);
	private readonly ConcurrentDictionary<TKey, string> _storage = new();

	/// <inheritdoc/>
	public string ProviderName => "Memory";

	/// <inheritdoc/>
	public bool IsPersistent => false;

	/// <inheritdoc/>
	public async Task StoreAsync<T>(TKey key, T obj, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003

		if (obj is null)
		{
			await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
			return;
		}

		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string serializedData = await _serializationProvider.SerializeAsync(obj, cancellationToken).ConfigureAwait(false);
			_storage.AddOrUpdate(key, serializedData, (_, _) => serializedData);
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to store object with key '{key}'", ex);
		}
	}

	/// <inheritdoc/>
	public async Task<T?> RetrieveAsync<T>(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_storage.TryGetValue(key, out string? serializedData) || serializedData is null)
			{
				return default;
			}

			T? obj = await _serializationProvider.DeserializeAsync<T>(serializedData, cancellationToken).ConfigureAwait(false);
			return obj;
		}
		catch (Exception ex)
		{
			throw new PersistenceProviderException($"Failed to retrieve object with key '{key}'", ex);
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
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();

		bool exists = _storage.ContainsKey(key);
		return Task.FromResult(exists);
	}

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
	{
#pragma warning disable KTSU0003 // Ensure.NotNull requires class constraint, but TKey is notnull
		ArgumentNullException.ThrowIfNull(key);
#pragma warning restore KTSU0003
		cancellationToken.ThrowIfCancellationRequested();

		bool removed = _storage.TryRemove(key, out _);
		return Task.FromResult(removed);
	}

	/// <inheritdoc/>
	public Task<IEnumerable<TKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IEnumerable<TKey> keys = [.. _storage.Keys]; // Create a snapshot to avoid concurrent modification issues
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
