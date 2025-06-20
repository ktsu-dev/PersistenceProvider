// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ktsu.SerializationProvider;

[TestClass]
public sealed class MemoryPersistenceProviderTests
{
	private MemoryPersistenceProvider<string>? _provider;
	private ISerializationProvider? _serializationProvider;

	[TestInitialize]
	public void Initialize()
	{
		// You would typically use a mock or a real implementation here
		// For this example, we'll need to assume a concrete implementation exists
		_serializationProvider = new MockSerializationProvider();
		_provider = new MemoryPersistenceProvider<string>(_serializationProvider);
	}

	[TestMethod]
	public async Task StoreAsync_ShouldStoreObject()
	{
		// Arrange
		TestModel testObject = new()
		{ Id = 1, Name = "Test" };
		const string key = "test-key";

		// Act
		await _provider!.StoreAsync(key, testObject).ConfigureAwait(false);

		// Assert
		bool exists = await _provider.ExistsAsync(key).ConfigureAwait(false);
		Assert.IsTrue(exists);
	}

	[TestMethod]
	public async Task RetrieveAsync_ShouldReturnStoredObject()
	{
		// Arrange
		TestModel testObject = new()
		{ Id = 1, Name = "Test" };
		const string key = "test-key";

		// Act
		await _provider!.StoreAsync(key, testObject).ConfigureAwait(false);
		TestModel? retrieved = await _provider.RetrieveAsync<TestModel>(key).ConfigureAwait(false);

		// Assert
		Assert.IsNotNull(retrieved);
		Assert.AreEqual(testObject.Id, retrieved.Id);
		Assert.AreEqual(testObject.Name, retrieved.Name);
	}

	[TestMethod]
	public async Task RetrieveAsync_ShouldReturnNullForNonExistentKey()
	{
		// Arrange
		const string key = "non-existent-key";

		// Act
		TestModel? retrieved = await _provider!.RetrieveAsync<TestModel>(key).ConfigureAwait(false);

		// Assert
		Assert.IsNull(retrieved);
	}

	[TestMethod]
	public async Task RetrieveOrCreateAsync_ShouldCreateNewObjectIfNotExists()
	{
		// Arrange
		const string key = "new-key";

		// Act
		TestModel retrieved = await _provider!.RetrieveOrCreateAsync<TestModel>(key).ConfigureAwait(false);

		// Assert
		Assert.IsNotNull(retrieved);
		Assert.AreEqual(0, retrieved.Id);
		Assert.IsNull(retrieved.Name);
	}

	[TestMethod]
	public async Task RemoveAsync_ShouldRemoveExistingObject()
	{
		// Arrange
		TestModel testObject = new()
		{ Id = 1, Name = "Test" };
		const string key = "test-key";

		// Act
		await _provider!.StoreAsync(key, testObject).ConfigureAwait(false);
		bool removed = await _provider.RemoveAsync(key).ConfigureAwait(false);
		bool exists = await _provider.ExistsAsync(key).ConfigureAwait(false);

		// Assert
		Assert.IsTrue(removed);
		Assert.IsFalse(exists);
	}

	[TestMethod]
	public async Task ClearAsync_ShouldRemoveAllObjects()
	{
		// Arrange
		TestModel testObject1 = new()
		{ Id = 1, Name = "Test1" };
		TestModel testObject2 = new()
		{ Id = 2, Name = "Test2" };

		// Act
		await _provider!.StoreAsync("key1", testObject1).ConfigureAwait(false);
		await _provider.StoreAsync("key2", testObject2).ConfigureAwait(false);
		await _provider.ClearAsync().ConfigureAwait(false);

		// Assert
		bool exists1 = await _provider.ExistsAsync("key1").ConfigureAwait(false);
		bool exists2 = await _provider.ExistsAsync("key2").ConfigureAwait(false);
		Assert.IsFalse(exists1);
		Assert.IsFalse(exists2);
	}

	[TestMethod]
	public void ProviderName_ShouldReturnMemory()
	{
		// Act & Assert
		Assert.AreEqual("Memory", _provider!.ProviderName);
	}

	[TestMethod]
	public void IsPersistent_ShouldReturnFalse()
	{
		// Act & Assert
		Assert.IsFalse(_provider!.IsPersistent);
	}

	private sealed class TestModel
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}

	private sealed class MockSerializationProvider : ISerializationProvider
	{
		public string ProviderName => "Mock";
		public string ContentType => "application/json";

		public string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj);
		public string Serialize(object obj, Type type) => System.Text.Json.JsonSerializer.Serialize(obj, type);
		public T Deserialize<T>(string data) => System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
		public object Deserialize(string data, Type type) => System.Text.Json.JsonSerializer.Deserialize(data, type)!;

		public Task<string> SerializeAsync<T>(T obj, CancellationToken cancellationToken = default) =>
			Task.FromResult(Serialize(obj));
		public Task<string> SerializeAsync(object obj, Type type, CancellationToken cancellationToken = default) =>
			Task.FromResult(Serialize(obj, type));
		public Task<T> DeserializeAsync<T>(string data, CancellationToken cancellationToken = default) =>
			Task.FromResult(Deserialize<T>(data));
		public Task<object> DeserializeAsync(string data, Type type, CancellationToken cancellationToken = default) =>
			Task.FromResult(Deserialize(data, type));
	}
}
