// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.PersistenceProvider.Test;

using ktsu.FileSystemProvider;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ktsu.SerializationProvider;

[TestClass]
public sealed class AppDataPersistenceProviderTests
{
	private AppDataPersistenceProvider<string>? _provider;
	private FileSystemProvider? _fileSystemProvider;
	private ISerializationProvider? _serializationProvider;
	private string _tempAppName = string.Empty;

	[TestInitialize]
	public void Initialize()
	{
		_fileSystemProvider = new FileSystemProvider();
		_serializationProvider = new MockSerializationProvider();
		_tempAppName = $"TestApp_{Guid.NewGuid():N}";
		_provider = new AppDataPersistenceProvider<string>(_fileSystemProvider, _serializationProvider, _tempAppName);
	}

	[TestCleanup]
	public void Cleanup()
	{
		// Clean up the temporary directory using the file system provider
		if (_fileSystemProvider is not null)
		{
			// Environment.GetFolderPath is a system API, not file I/O
			string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string testPath = _fileSystemProvider.Current.Path.Combine(appDataPath, _tempAppName);
			if (_fileSystemProvider.Current.Directory.Exists(testPath))
			{
				_fileSystemProvider.Current.Directory.Delete(testPath, true);
			}
		}
	}

	[TestMethod]
	public async Task StoreAsync_ShouldStoreObject()
	{
		// Arrange
		TestModel testObject = new() { Id = 1, Name = "Test" };
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
		TestModel testObject = new() { Id = 1, Name = "Test" };
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
		TestModel testObject = new() { Id = 1, Name = "Test" };
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
	public async Task GetAllKeysAsync_ShouldReturnAllStoredKeys()
	{
		// Arrange
		TestModel testObject1 = new() { Id = 1, Name = "Test1" };
		TestModel testObject2 = new() { Id = 2, Name = "Test2" };

		// Act
		await _provider!.StoreAsync("key1", testObject1).ConfigureAwait(false);
		await _provider.StoreAsync("key2", testObject2).ConfigureAwait(false);
		IEnumerable<string> keys = await _provider.GetAllKeysAsync().ConfigureAwait(false);

		// Assert
		List<string> keysList = [.. keys];
		Assert.AreEqual(2, keysList.Count);
		Assert.IsTrue(keysList.Contains("key1"));
		Assert.IsTrue(keysList.Contains("key2"));
	}

	[TestMethod]
	public async Task ClearAsync_ShouldRemoveAllObjects()
	{
		// Arrange
		TestModel testObject1 = new() { Id = 1, Name = "Test1" };
		TestModel testObject2 = new() { Id = 2, Name = "Test2" };

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
	public void ProviderName_ShouldReturnAppData()
	{
		// Act & Assert
		Assert.AreEqual("AppData", _provider!.ProviderName);
	}

	[TestMethod]
	public void IsPersistent_ShouldReturnTrue()
	{
		// Act & Assert
		Assert.IsTrue(_provider!.IsPersistent);
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
