// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider.Test;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SerializationProvider;

[TestClass]
public sealed class MemoryPersistenceProviderTests
{
	private IPersistenceProvider<string>? _provider;
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
		var testObject = new TestModel { Id = 1, Name = "Test" };
		const string key = "test-key";

		// Act
		await _provider!.StoreAsync(key, testObject);

		// Assert
		bool exists = await _provider.ExistsAsync(key);
		Assert.IsTrue(exists);
	}

	[TestMethod]
	public async Task RetrieveAsync_ShouldReturnStoredObject()
	{
		// Arrange
		var testObject = new TestModel { Id = 1, Name = "Test" };
		const string key = "test-key";

		// Act
		await _provider!.StoreAsync(key, testObject);
		var retrieved = await _provider.RetrieveAsync<TestModel>(key);

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
		var retrieved = await _provider!.RetrieveAsync<TestModel>(key);

		// Assert
		Assert.IsNull(retrieved);
	}

	[TestMethod]
	public async Task RetrieveOrCreateAsync_ShouldCreateNewObjectIfNotExists()
	{
		// Arrange
		const string key = "new-key";

		// Act
		var retrieved = await _provider!.RetrieveOrCreateAsync<TestModel>(key);

		// Assert
		Assert.IsNotNull(retrieved);
		Assert.AreEqual(0, retrieved.Id);
		Assert.IsNull(retrieved.Name);
	}

	[TestMethod]
	public async Task RemoveAsync_ShouldRemoveExistingObject()
	{
		// Arrange
		var testObject = new TestModel { Id = 1, Name = "Test" };
		const string key = "test-key";

		// Act
		await _provider!.StoreAsync(key, testObject);
		bool removed = await _provider.RemoveAsync(key);
		bool exists = await _provider.ExistsAsync(key);

		// Assert
		Assert.IsTrue(removed);
		Assert.IsFalse(exists);
	}

	[TestMethod]
	public async Task ClearAsync_ShouldRemoveAllObjects()
	{
		// Arrange
		var testObject1 = new TestModel { Id = 1, Name = "Test1" };
		var testObject2 = new TestModel { Id = 2, Name = "Test2" };

		// Act
		await _provider!.StoreAsync("key1", testObject1);
		await _provider.StoreAsync("key2", testObject2);
		await _provider.ClearAsync();

		// Assert
		bool exists1 = await _provider.ExistsAsync("key1");
		bool exists2 = await _provider.ExistsAsync("key2");
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