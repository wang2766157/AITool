using Microsoft.Extensions.VectorData;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Numerics.Tensors;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CT.AI.Agent.Model.AI;

public class CloudService
{
    [VectorStoreRecordKey]
    public int Key { get; set; }
    [VectorStoreRecordData]
    public string Name { get; set; }
    [VectorStoreRecordData]
    public string Description { get; set; }
    [VectorStoreRecordVector(384, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

public sealed class InMemoryVectorStore //: IVectorStore
{
    /// <summary> 记录集合的内部存储 Internal storage for the record collection. </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, object>> _internalCollection;
    /// <summary> 每个集合限定单一数据类型 The data type of each collection, to enforce a single type per collection. </summary>
    private readonly ConcurrentDictionary<string, Type> _internalCollectionTypes = new();
    /// <summary>
    /// 构造函数 <see cref="InMemoryVectorStore"/>
    /// </summary>
    public InMemoryVectorStore() => _internalCollection = new();
    /// <summary>
    /// 构造函数 <see cref="InMemoryVectorStore"/>
    /// </summary>
    /// <param name="internalCollection"> 允许传入存储字典以供测试 Allows passing in the dictionary used for storage, for testing purposes. </param>
    internal InMemoryVectorStore(ConcurrentDictionary<string, ConcurrentDictionary<object, object>> internalCollection)
        => _internalCollection = internalCollection;
    /// <inheritdoc />
    public IVectorStoreRecordCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreRecordDefinition? vectorStoreRecordDefinition = null) where TKey : notnull
    {
        if (_internalCollectionTypes.TryGetValue(name, out var existingCollectionDataType) && existingCollectionDataType != typeof(TRecord))
            throw new InvalidOperationException($"Collection '{name}' already exists and with data type '{existingCollectionDataType.Name}' so cannot be re-created with data type '{typeof(TRecord).Name}'.");
        var collection = new InMemoryVectorStoreRecordCollection<TKey, TRecord>(
            _internalCollection, _internalCollectionTypes, name,
            new() { VectorStoreRecordDefinition = vectorStoreRecordDefinition }) as IVectorStoreRecordCollection<TKey, TRecord>;
        return collection!;
    }
    /// <inheritdoc />
    public IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
        => _internalCollection.Keys.ToAsyncEnumerable();
}
public sealed class InMemoryVectorStoreRecordCollection<TKey, TRecord> : IVectorStoreRecordCollection<TKey, TRecord> where TKey : notnull
{
    /// <summary> 模型向量的可能类型集合 A set of types that vectors on the provided model may have. </summary>
    private static readonly HashSet<Type> s_supportedVectorTypes =
    [
        typeof(ReadOnlyMemory<float>),
        typeof(ReadOnlyMemory<float>?),
    ];
    /// <summary> 向量搜索中，默认选项 The default options for vector search. </summary>
    private static readonly VectorSearchOptions<TRecord> s_defaultVectorSearchOptions = new();
    /// <summary> 所有记录集合的内部存储 Internal storage for all of the record collections. </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, object>> _internalCollections;
    /// <summary>The data type of each collection, to enforce a single type per collection.</summary>
    private readonly ConcurrentDictionary<string, Type> _internalCollectionTypes;
    /// <summary>Optional configuration options for this class.</summary>
    private readonly InMemoryVectorStoreRecordCollectionOptions<TKey, TRecord> _options;
    /// <summary>The name of the collection that this <see cref="InMemoryVectorStoreRecordCollection{TKey,TRecord}"/> will access.</summary>
    private readonly string _collectionName;
    /// <summary>A helper to access property information for the current data model and record definition.</summary>
    private readonly VectorStoreRecordPropertyReader _propertyReader;
    /// <summary>A dictionary of vector properties on the provided model, keyed by the property name.</summary>
    private readonly Dictionary<string, VectorStoreRecordVectorProperty> _vectorProperties;
    /// <summary>An function to look up vectors from the records.</summary>
    private readonly InMemoryVectorStoreVectorResolver<TRecord> _vectorResolver;
    /// <summary>An function to look up keys from the records.</summary>
    private readonly InMemoryVectorStoreKeyResolver<TKey, TRecord> _keyResolver;
    /// <summary>
    /// 构造函数 <see cref="InMemoryVectorStoreRecordCollection{TKey,TRecord}"/>
    /// </summary>
    /// <param name="collectionName">The name of the collection that this <see cref="InMemoryVectorStoreRecordCollection{TKey,TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public InMemoryVectorStoreRecordCollection(string collectionName, InMemoryVectorStoreRecordCollectionOptions<TKey, TRecord>? options = default)
    {
        // Verify.
        //Verify.NotNullOrWhiteSpace(collectionName);
        VectorStoreRecordPropertyVerification.VerifyGenericDataModelDefinitionSupplied(typeof(TRecord), options?.VectorStoreRecordDefinition is not null);
        // Assign.
        _collectionName = collectionName;
        _internalCollections = new();
        _internalCollectionTypes = new();
        _options = options ?? new InMemoryVectorStoreRecordCollectionOptions<TKey, TRecord>();
        _propertyReader = new VectorStoreRecordPropertyReader(typeof(TRecord), _options.VectorStoreRecordDefinition, new() { RequiresAtLeastOneVector = false, SupportsMultipleKeys = false, SupportsMultipleVectors = true });
        // Validate property types.
        _propertyReader.VerifyVectorProperties(s_supportedVectorTypes);
        _vectorProperties = _propertyReader.VectorProperties.ToDictionary(x => x.DataModelPropertyName);
        // Assign resolvers.
        _vectorResolver = CreateVectorResolver(_options.VectorResolver, _vectorProperties);
        _keyResolver = CreateKeyResolver(_options.KeyResolver, _propertyReader.KeyProperty);
    }
    /// <summary>
    /// 构造函数 <see cref="InMemoryVectorStoreRecordCollection{TKey,TRecord}"/>
    /// </summary>
    /// <param name="internalCollection">Internal storage for the record collection.</param>
    /// <param name="internalCollectionTypes">The data type of each collection, to enforce a single type per collection.</param>
    /// <param name="collectionName">The name of the collection that this <see cref="InMemoryVectorStoreRecordCollection{TKey,TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    internal InMemoryVectorStoreRecordCollection(ConcurrentDictionary<string, ConcurrentDictionary<object, object>> internalCollection,
        ConcurrentDictionary<string, Type> internalCollectionTypes, string collectionName, InMemoryVectorStoreRecordCollectionOptions<TKey, TRecord>? options = default)
        : this(collectionName, options)
    {
        _internalCollections = internalCollection;
        _internalCollectionTypes = internalCollectionTypes;
    }
    /// <inheritdoc />
    public string CollectionName => _collectionName;
    /// <inheritdoc />
    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default) =>
         _internalCollections.ContainsKey(_collectionName) ? Task.FromResult(true) : Task.FromResult(false);
    /// <inheritdoc />
    public Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (!_internalCollections.ContainsKey(_collectionName)
            && _internalCollections.TryAdd(_collectionName, new ConcurrentDictionary<object, object>())
            && _internalCollectionTypes.TryAdd(_collectionName, typeof(TRecord)))
            return Task.CompletedTask;
        return Task.FromException(new VectorStoreOperationException("Collection already exists.")
        {
            VectorStoreType = "InMemory",
            CollectionName = CollectionName,
            OperationName = "CreateCollection"
        });
    }
    /// <inheritdoc />
    public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
            await CreateCollectionAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        _internalCollections.TryRemove(_collectionName, out _);
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = GetCollectionDictionary();
        if (collectionDictionary.TryGetValue(key, out var record))
            return Task.FromResult<TRecord?>((TRecord?)record);
        return Task.FromResult<TRecord?>(default);
    }
    /// <inheritdoc />
    public async IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var record = await GetAsync(key, options, cancellationToken).ConfigureAwait(false);
            if (record is not null)
                yield return record;
        }
    }
    /// <inheritdoc />
    public Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = GetCollectionDictionary();
        collectionDictionary.TryRemove(key, out _);
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public Task DeleteBatchAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        var collectionDictionary = GetCollectionDictionary();
        foreach (var key in keys)
            collectionDictionary.TryRemove(key, out _);
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public Task<TKey> UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        //Verify.NotNull(record);
        var collectionDictionary = GetCollectionDictionary();
        var key = (TKey)_keyResolver(record)!;
        collectionDictionary.AddOrUpdate(key!, record, (key, currentValue) => record);
        return Task.FromResult(key!);
    }
    /// <inheritdoc />
    public async IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
            yield return await UpsertAsync(record, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<VectorSearchResults<TRecord>> VectorizedSearchAsync<TVector>(TVector vector, VectorSearchOptions<TRecord>? options = null, CancellationToken cancellationToken = default)
    {
        //Verify.NotNull(vector);
        if (vector is not ReadOnlyMemory<float> floatVector)
            throw new NotSupportedException($"The provided vector type {vector.GetType().FullName} is not supported by the InMemory Vector Store.");
        // Resolve options and get requested vector property or first as default.
        var internalOptions = options ?? s_defaultVectorSearchOptions;
        var vectorProperty = _propertyReader.GetVectorPropertyOrSingle(internalOptions);
        // Filter records using the provided filter before doing the vector comparison.
        var allValues = GetCollectionDictionary().Values.Cast<TRecord>();
        var filteredRecords = internalOptions switch
        {
            { OldFilter: not null, Filter: not null } => throw new ArgumentException("Either Filter or OldFilter can be specified, but not both"),
            { OldFilter: VectorSearchFilter legacyFilter } => InMemoryVectorStoreCollectionSearchMapping.FilterRecords(legacyFilter, allValues),
            { Filter: Expression<Func<TRecord, bool>> newFilter } => allValues.AsQueryable().Where(newFilter),
            _ => allValues
        };
        // Compare each vector in the filtered results with the provided vector.
        var results = filteredRecords.Select<TRecord, (TRecord record, float score)?>(record =>
        {
            var vectorObject = _vectorResolver(vectorProperty.DataModelPropertyName!, record);
            if (vectorObject is not ReadOnlyMemory<float> dbVector) return null;
            var score = InMemoryVectorStoreCollectionSearchMapping.CompareVectors(floatVector.Span, dbVector.Span, vectorProperty.DistanceFunction);
            var convertedscore = InMemoryVectorStoreCollectionSearchMapping.ConvertScore(score, vectorProperty.DistanceFunction);
            return (record, convertedscore);
        });
        // Get the non-null results since any record with a null vector results in a null result.
        var nonNullResults = results.Where(x => x.HasValue).Select(x => x!.Value);
        // Calculate the total results count if requested.
        long? count = null;
        if (internalOptions.IncludeTotalCount)
            count = nonNullResults.Count();
        // Sort the results appropriately for the selected distance function and get the right page of results .
        var sortedScoredResults = InMemoryVectorStoreCollectionSearchMapping.ShouldSortDescending(vectorProperty.DistanceFunction) ?
            nonNullResults.OrderByDescending(x => x.score) :
            nonNullResults.OrderBy(x => x.score);
        var resultsPage = sortedScoredResults.Skip(internalOptions.Skip).Take(internalOptions.Top);
        // Build the response.
        var t1 = resultsPage.Select(x => new VectorSearchResult<TRecord>(x.record, x.score));
        var vectorSearchResultList = t1.ToAsyncEnumerable();
        return new VectorSearchResults<TRecord>(vectorSearchResultList) { TotalCount = count };
    }
    /// <summary>
    /// Get the collection dictionary from the internal storage, throws if it does not exist.
    /// </summary>
    /// <returns>The retrieved collection dictionary.</returns>
    internal ConcurrentDictionary<object, object> GetCollectionDictionary()
    {
        if (!_internalCollections.TryGetValue(_collectionName, out var collectionDictionary))
            throw new VectorStoreOperationException($"Call to vector store failed. Collection '{_collectionName}' does not exist.");
        return collectionDictionary;
    }
    /// <summary>
    /// Pick / create a vector resolver that will read a vector from a record in the store based on the vector name.
    /// 1. If an override resolver is provided, use that.
    /// 2. If the record type is <see cref="VectorStoreGenericDataModel{TKey}"/> create a resolver that looks up the vector in its <see cref="VectorStoreGenericDataModel{TKey}.Vectors"/> dictionary.
    /// 3. Otherwise, create a resolver that assumes the vector is a property directly on the record and use the record definition to determine the name.
    /// </summary>
    /// <param name="overrideVectorResolver">The override vector resolver if one was provided.</param>
    /// <param name="vectorProperties">A dictionary of vector properties from the record definition.</param>
    /// <returns>The <see cref="InMemoryVectorStoreVectorResolver{TRecord}"/>.</returns>
    private static InMemoryVectorStoreVectorResolver<TRecord> CreateVectorResolver(InMemoryVectorStoreVectorResolver<TRecord>? overrideVectorResolver, Dictionary<string, VectorStoreRecordVectorProperty> vectorProperties)
    {
        // Custom resolver.
        if (overrideVectorResolver is not null)
            return overrideVectorResolver;
        // Generic data model resolver.
        if (typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>))
        {
            return (vectorName, record) =>
            {
                var genericDataModelRecord = record as VectorStoreGenericDataModel<TKey>;
                var vectorsDictionary = genericDataModelRecord!.Vectors;
                if (vectorsDictionary != null && vectorsDictionary.TryGetValue(vectorName, out var vector))
                    return vector;
                throw new InvalidOperationException($"The collection does not have a vector field named '{vectorName}', so vector search is not possible.");
            };
        }
        // Default resolver.
        var vectorPropertiesInfo = vectorProperties.Values
            .Select(x => x.DataModelPropertyName)
            .Select(x => typeof(TRecord).GetProperty(x) ?? throw new ArgumentException($"Vector property '{x}' was not found on {typeof(TRecord).Name}"))
            .ToDictionary(x => x.Name);
        return (vectorName, record) =>
        {
            if (vectorPropertiesInfo.TryGetValue(vectorName, out var vectorPropertyInfo)) return vectorPropertyInfo.GetValue(record);
            throw new InvalidOperationException($"The collection does not have a vector field named '{vectorName}', so vector search is not possible.");
        };
    }
    /// <summary>
    /// Pick / create a key resolver that will read a key from a record in the store.
    /// 1. If an override resolver is provided, use that.
    /// 2. If the record type is <see cref="VectorStoreGenericDataModel{TKey}"/> create a resolver that reads the Key property from it.
    /// 3. Otherwise, create a resolver that assumes the key is a property directly on the record and use the record definition to determine the name.
    /// </summary>
    /// <param name="overrideKeyResolver">The override key resolver if one was provided.</param>
    /// <param name="keyProperty">They key property from the record definition.</param>
    /// <returns>The <see cref="InMemoryVectorStoreKeyResolver{TKey, TRecord}"/>.</returns>
    private static InMemoryVectorStoreKeyResolver<TKey, TRecord> CreateKeyResolver(InMemoryVectorStoreKeyResolver<TKey, TRecord>? overrideKeyResolver, VectorStoreRecordKeyProperty keyProperty)
    {
        // Custom resolver.
        if (overrideKeyResolver is not null) return overrideKeyResolver;
        // Generic data model resolver.
        if (typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>))
        {
            return (record) =>
            {
                var genericDataModelRecord = record as VectorStoreGenericDataModel<TKey>;
                return genericDataModelRecord!.Key;
            };
        }
        // Default resolver.
        var keyPropertyInfo = typeof(TRecord).GetProperty(keyProperty.DataModelPropertyName) ?? throw new ArgumentException($"Key property {keyProperty.DataModelPropertyName} not found on {typeof(TRecord).Name}");
        return (record) => (TKey)keyPropertyInfo.GetValue(record)!;
    }
}
public sealed class InMemoryVectorStoreRecordCollectionOptions<TKey, TRecord> where TKey : notnull
{
    /// <summary>
    /// Gets or sets an optional record definition that defines the schema of the record type.
    /// </summary>
    /// <remarks>
    /// If not provided, the schema will be inferred from the record model class using reflection.
    /// In this case, the record model properties must be annotated with the appropriate attributes to indicate their usage.
    /// See <see cref="VectorStoreRecordKeyAttribute"/>, <see cref="VectorStoreRecordDataAttribute"/> and <see cref="VectorStoreRecordVectorAttribute"/>.
    /// </remarks>
    public VectorStoreRecordDefinition? VectorStoreRecordDefinition { get; init; } = null;

    /// <summary>
    /// An optional function that can be used to look up vectors from a record.
    /// </summary>
    /// <remarks>
    /// If not provided, the default behavior is to look for direct properties of the record
    /// using reflection. This delegate can be used to provide a custom implementation if
    /// the vector properties are located somewhere else on the record.
    /// </remarks>
    public InMemoryVectorStoreVectorResolver<TRecord>? VectorResolver { get; init; } = null;

    /// <summary>
    /// An optional function that can be used to look up record keys.
    /// </summary>
    /// <remarks>
    /// If not provided, the default behavior is to look for a direct property of the record
    /// using reflection. This delegate can be used to provide a custom implementation if
    /// the key property is located somewhere else on the record.
    /// </remarks>
    public InMemoryVectorStoreKeyResolver<TKey, TRecord>? KeyResolver { get; init; } = null;
}
public sealed class VectorStoreRecordDefinition
{
    /// <summary>Empty static list for initialization purposes.</summary>
    private static readonly List<VectorStoreRecordProperty> s_emptyFields = new();

    /// <summary>
    /// Gets or sets the list of properties that are stored in the record.
    /// </summary>
    public IReadOnlyList<VectorStoreRecordProperty> Properties { get; init; } = s_emptyFields;
}
public delegate object? InMemoryVectorStoreVectorResolver<TRecord>(string vectorName, TRecord record);
public delegate TKey? InMemoryVectorStoreKeyResolver<TKey, TRecord>(TRecord record) where TKey : notnull;
[ExcludeFromCodeCoverage]
internal static class VectorStoreRecordPropertyVerification
{
    /// <summary>
    /// Verify that the given properties are of the supported types.
    /// </summary>
    /// <param name="properties">The properties to check.</param>
    /// <param name="supportedTypes">A set of supported types that the provided properties may have.</param>
    /// <param name="propertyCategoryDescription">A description of the category of properties being checked. Used for error messaging.</param>
    /// <param name="supportEnumerable">A value indicating whether <see cref="IEnumerable{T}"/> versions of all the types should also be supported.</param>
    /// <exception cref="ArgumentException">Thrown if any of the properties are not in the given set of types.</exception>
    public static void VerifyPropertyTypes(List<PropertyInfo> properties, HashSet<Type> supportedTypes, string propertyCategoryDescription, bool? supportEnumerable = false)
    {
        var supportedEnumerableElementTypes = supportEnumerable == true
            ? supportedTypes
            : [];

        VerifyPropertyTypes(properties, supportedTypes, supportedEnumerableElementTypes, propertyCategoryDescription);
    }

    /// <summary>
    /// Verify that the given properties are of the supported types.
    /// </summary>
    /// <param name="properties">The properties to check.</param>
    /// <param name="supportedTypes">A set of supported types that the provided properties may have.</param>
    /// <param name="supportedEnumerableElementTypes">A set of supported types that the provided enumerable properties may use as their element type.</param>
    /// <param name="propertyCategoryDescription">A description of the category of properties being checked. Used for error messaging.</param>
    /// <exception cref="ArgumentException">Thrown if any of the properties are not in the given set of types.</exception>
    public static void VerifyPropertyTypes(List<PropertyInfo> properties, HashSet<Type> supportedTypes, HashSet<Type> supportedEnumerableElementTypes, string propertyCategoryDescription)
    {
        foreach (var property in properties)
        {
            VerifyPropertyType(property.Name, property.PropertyType, supportedTypes, supportedEnumerableElementTypes, propertyCategoryDescription);
        }
    }

    /// <summary>
    /// Verify that the given properties are of the supported types.
    /// </summary>
    /// <param name="properties">The properties to check.</param>
    /// <param name="supportedTypes">A set of supported types that the provided properties may have.</param>
    /// <param name="propertyCategoryDescription">A description of the category of properties being checked. Used for error messaging.</param>
    /// <param name="supportEnumerable">A value indicating whether <see cref="IEnumerable{T}"/> versions of all the types should also be supported.</param>
    /// <exception cref="ArgumentException">Thrown if any of the properties are not in the given set of types.</exception>
    public static void VerifyPropertyTypes(IEnumerable<VectorStoreRecordProperty> properties, HashSet<Type> supportedTypes, string propertyCategoryDescription, bool? supportEnumerable = false)
    {
        var supportedEnumerableElementTypes = supportEnumerable == true
            ? supportedTypes
            : [];

        VerifyPropertyTypes(properties, supportedTypes, supportedEnumerableElementTypes, propertyCategoryDescription);
    }

    /// <summary>
    /// Verify that the given properties are of the supported types.
    /// </summary>
    /// <param name="properties">The properties to check.</param>
    /// <param name="supportedTypes">A set of supported types that the provided properties may have.</param>
    /// <param name="supportedEnumerableElementTypes">A set of supported types that the provided enumerable properties may use as their element type.</param>
    /// <param name="propertyCategoryDescription">A description of the category of properties being checked. Used for error messaging.</param>
    /// <exception cref="ArgumentException">Thrown if any of the properties are not in the given set of types.</exception>
    public static void VerifyPropertyTypes(IEnumerable<VectorStoreRecordProperty> properties, HashSet<Type> supportedTypes, HashSet<Type> supportedEnumerableElementTypes, string propertyCategoryDescription)
    {
        foreach (var property in properties)
        {
            VerifyPropertyType(property.DataModelPropertyName, property.PropertyType, supportedTypes, supportedEnumerableElementTypes, propertyCategoryDescription);
        }
    }

    /// <summary>
    /// Verify that the given property is of the supported types.
    /// </summary>
    /// <param name="propertyName">The name of the property being checked. Used for error messaging.</param>
    /// <param name="propertyType">The type of the property being checked.</param>
    /// <param name="supportedTypes">A set of supported types that the provided property may have.</param>
    /// <param name="supportedEnumerableElementTypes">A set of supported types that the provided property may use as its element type if it's enumerable.</param>
    /// <param name="propertyCategoryDescription">A description of the category of property being checked. Used for error messaging.</param>
    /// <exception cref="ArgumentException">Thrown if the property is not in the given set of types.</exception>
    public static void VerifyPropertyType(string propertyName, Type propertyType, HashSet<Type> supportedTypes, HashSet<Type> supportedEnumerableElementTypes, string propertyCategoryDescription)
    {
        // Add shortcut before testing all the more expensive scenarios.
        if (supportedTypes.Contains(propertyType))
        {
            return;
        }

        // Check all collection scenarios and get stored type.
        if (supportedEnumerableElementTypes.Count > 0 && IsSupportedEnumerableType(propertyType))
        {
            var typeToCheck = GetCollectionElementType(propertyType);

            if (!supportedEnumerableElementTypes.Contains(typeToCheck))
            {
                var supportedEnumerableElementTypesString = string.Join(", ", supportedEnumerableElementTypes!.Select(t => t.FullName));
                throw new ArgumentException($"Enumerable {propertyCategoryDescription} properties must have one of the supported element types: {supportedEnumerableElementTypesString}. Element type of the property '{propertyName}' is {typeToCheck.FullName}.");
            }
        }
        else
        {
            // if we got here, we know the type is not supported
            var supportedTypesString = string.Join(", ", supportedTypes.Select(t => t.FullName));
            throw new ArgumentException($"{propertyCategoryDescription} properties must be one of the supported types: {supportedTypesString}. Type of the property '{propertyName}' is {propertyType.FullName}.");
        }
    }

    /// <summary>
    /// Verify if the provided type is one of the supported Enumerable types.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><see langword="true"/> if the type is a supported Enumerable, <see langword="false"/> otherwise.</returns>
    public static bool IsSupportedEnumerableType(Type type)
    {
        if (type.IsArray || type == typeof(IEnumerable))
        {
            return true;
        }

#if NET6_0_OR_GREATER
        if (typeof(IList).IsAssignableFrom(type) && type.GetMemberWithSameMetadataDefinitionAs(s_objectGetDefaultConstructorInfo) != null)
#else
        if (typeof(IList).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null)
#endif
        {
            return true;
        }

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ICollection<>) ||
                genericTypeDefinition == typeof(IEnumerable<>) ||
                genericTypeDefinition == typeof(IList<>) ||
                genericTypeDefinition == typeof(IReadOnlyCollection<>) ||
                genericTypeDefinition == typeof(IReadOnlyList<>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see cref="Type"/> of collection elements.
    /// </summary>
    public static Type GetCollectionElementType(Type collectionType)
    {
        return collectionType switch
        {
            IEnumerable => typeof(object),
            var enumerableType when GetGenericEnumerableInterface(enumerableType) is Type enumerableInterface => enumerableInterface.GetGenericArguments()[0],
            var arrayType when arrayType.IsArray => arrayType.GetElementType()!,
            _ => collectionType
        };
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
        Justification = "The 'IEnumerable<>' Type must exist and so trimmer kept it. In which case " +
            "It also kept it on any type which implements it. The below call to GetInterfaces " +
            "may return fewer results when trimmed but it will return 'IEnumerable<>' " +
            "if the type implemented it, even after trimming.")]
    private static Type? GetGenericEnumerableInterface(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type;
        }

        foreach (Type typeToCheck in type.GetInterfaces())
        {
            if (typeToCheck.IsGenericType && typeToCheck.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return typeToCheck;
            }
        }

        return null;
    }

    internal static bool IsGenericDataModel(Type recordType)
        => recordType.IsGenericType && recordType.GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>);

    /// <summary>
    /// Checks that if the provided <paramref name="recordType"/> is a <see cref="VectorStoreGenericDataModel{T}"/> that the key type is supported by the default mappers.
    /// If not supported, a custom mapper must be supplied, otherwise an exception is thrown.
    /// </summary>
    /// <param name="recordType">The type of the record data model used by the connector.</param>
    /// <param name="customMapperSupplied">A value indicating whether a custom mapper was supplied to the connector</param>
    /// <param name="allowedKeyTypes">The list of key types supported by the default mappers.</param>
    /// <exception cref="ArgumentException">Thrown if the key type of the <see cref="VectorStoreGenericDataModel{T}"/> is not supported by the default mappers and a custom mapper was not supplied.</exception>
    public static void VerifyGenericDataModelKeyType(Type recordType, bool customMapperSupplied, IEnumerable<Type> allowedKeyTypes)
    {
        // If we are not dealing with a generic data model, no need to check anything else.
        if (!IsGenericDataModel(recordType))
        {
            return;
        }

        // If the key type is supported, we are good.
        var keyType = recordType.GetGenericArguments()[0];
        if (allowedKeyTypes.Contains(keyType))
        {
            return;
        }

        // If the key type is not supported out of the box, but a custom mapper was supplied, we are good.
        if (customMapperSupplied)
        {
            return;
        }

        throw new ArgumentException($"The key type '{keyType.FullName}' of data model '{nameof(VectorStoreGenericDataModel<string>)}' is not supported by the default mappers. " +
            $"Only the following key types are supported: {string.Join(", ", allowedKeyTypes)}. Please provide your own mapper to map to your chosen key type.");
    }

    /// <summary>
    /// Checks that if the provided <paramref name="recordType"/> is a <see cref="VectorStoreGenericDataModel{T}"/> that a <see cref="VectorStoreRecordDefinition"/> is also provided.
    /// </summary>
    /// <param name="recordType">The type of the record data model used by the connector.</param>
    /// <param name="recordDefinitionSupplied">A value indicating whether a record definition was supplied to the connector.</param>
    /// <exception cref="ArgumentException">Thrown if a <see cref="VectorStoreRecordDefinition"/> is not provided when using <see cref="VectorStoreGenericDataModel{T}"/>.</exception>
    public static void VerifyGenericDataModelDefinitionSupplied(Type recordType, bool recordDefinitionSupplied)
    {
        // If we are not dealing with a generic data model, no need to check anything else.
        if (!recordType.IsGenericType || recordType.GetGenericTypeDefinition() != typeof(VectorStoreGenericDataModel<>))
        {
            return;
        }

        // If we are dealing with a generic data model, and a record definition was supplied, we are good.
        if (recordDefinitionSupplied)
        {
            return;
        }

        throw new ArgumentException($"A {nameof(VectorStoreRecordDefinition)} must be provided when using '{nameof(VectorStoreGenericDataModel<string>)}'.");
    }

#if NET6_0_OR_GREATER
    private static readonly ConstructorInfo s_objectGetDefaultConstructorInfo = typeof(object).GetConstructor(Type.EmptyTypes)!;
#endif
}
[ExcludeFromCodeCoverage]
internal sealed class VectorStoreRecordPropertyReader
{
    /// <summary>The <see cref="Type"/> of the data model.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _dataModelType;
    /// <summary>A definition of the current storage model.</summary>
    private readonly VectorStoreRecordDefinition _vectorStoreRecordDefinition;
    /// <summary>Options for configuring the behavior of this class.</summary>
    private readonly VectorStoreRecordPropertyReaderOptions _options;
    /// <summary>The key properties from the definition.</summary>
    private readonly List<VectorStoreRecordKeyProperty> _keyProperties;
    /// <summary>The data properties from the definition.</summary>
    private readonly List<VectorStoreRecordDataProperty> _dataProperties;
    /// <summary>The vector properties from the definition.</summary>
    private readonly List<VectorStoreRecordVectorProperty> _vectorProperties;
    /// <summary>The <see cref="ConstructorInfo"/> of the parameterless constructor from the data model if one exists.</summary>
    private readonly Lazy<ConstructorInfo> _parameterlessConstructorInfo;
    /// <summary>The key <see cref="PropertyInfo"/> objects from the data model.</summary>
    private List<PropertyInfo>? _keyPropertiesInfo;
    /// <summary>The data <see cref="PropertyInfo"/> objects from the data model.</summary>
    private List<PropertyInfo>? _dataPropertiesInfo;
    /// <summary>The vector <see cref="PropertyInfo"/> objects from the data model.</summary>
    private List<PropertyInfo>? _vectorPropertiesInfo;
    /// <summary>A lazy initialized map of data model property names to the names under which they are stored in the data store.</summary>
    private readonly Lazy<Dictionary<string, string>> _storagePropertyNamesMap;
    /// <summary>A lazy initialized list of storage names of key properties.</summary>
    private readonly Lazy<List<string>> _keyPropertyStoragePropertyNames;
    /// <summary>A lazy initialized list of storage names of data properties.</summary>
    private readonly Lazy<List<string>> _dataPropertyStoragePropertyNames;
    /// <summary>A lazy initialized list of storage names of vector properties.</summary>
    private readonly Lazy<List<string>> _vectorPropertyStoragePropertyNames;
    /// <summary>A lazy initialized map of data model property names to the names they will have if serialized to JSON.</summary>
    private readonly Lazy<Dictionary<string, string>> _jsonPropertyNamesMap;
    /// <summary>A lazy initialized list of json names of key properties.</summary>
    private readonly Lazy<List<string>> _keyPropertyJsonNames;
    /// <summary>A lazy initialized list of json names of data properties.</summary>
    private readonly Lazy<List<string>> _dataPropertyJsonNames;
    /// <summary>A lazy initialized list of json names of vector properties.</summary>
    private readonly Lazy<List<string>> _vectorPropertyJsonNames;
    public VectorStoreRecordPropertyReader(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] Type dataModelType,
        VectorStoreRecordDefinition? vectorStoreRecordDefinition, VectorStoreRecordPropertyReaderOptions? options)
    {
        _dataModelType = dataModelType;
        _options = options ?? new VectorStoreRecordPropertyReaderOptions();
        // If a definition is provided, use it. Otherwise, create one from the type.
        if (vectorStoreRecordDefinition is not null)
        {
            // Here we received a definition, which gives us all of the information we need.
            // Some mappers though need to set properties on the data model using reflection
            // so we may still need to find the PropertyInfo objects on the data model later if required.
            _vectorStoreRecordDefinition = vectorStoreRecordDefinition;
        }
        else
        {
            // Here we didn't receive a definition, so we need to derive the information from
            // the data model. Since we may need the PropertyInfo objects later to read or write
            // property values on the data model, we save them for later in case we need them.
            var propertiesInfo = FindPropertiesInfo(dataModelType);
            _vectorStoreRecordDefinition = CreateVectorStoreRecordDefinitionFromType(propertiesInfo);
            _keyPropertiesInfo = propertiesInfo.KeyProperties;
            _dataPropertiesInfo = propertiesInfo.DataProperties;
            _vectorPropertiesInfo = propertiesInfo.VectorProperties;
        }
        // Verify the definition to make sure it does not have too many or too few of each property type.
        (_keyProperties, _dataProperties, _vectorProperties) = SplitDefinitionAndVerify(
            dataModelType.Name, _vectorStoreRecordDefinition,
            _options.SupportsMultipleKeys, _options.SupportsMultipleVectors,
            _options.RequiresAtLeastOneVector);
        // Setup lazy initializers.
        _storagePropertyNamesMap = new Lazy<Dictionary<string, string>>(() =>
        {
            return BuildPropertyNameToStorageNameMap((_keyProperties, _dataProperties, _vectorProperties));
        });
        _parameterlessConstructorInfo = new Lazy<ConstructorInfo>(() =>
        {
            var constructor = dataModelType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new ArgumentException($"Type {dataModelType.FullName} must have a parameterless constructor.");
            return constructor;
        });
        _keyPropertyStoragePropertyNames = new Lazy<List<string>>(() =>
        {
            var storagePropertyNames = _storagePropertyNamesMap.Value;
            return _keyProperties.Select(x => storagePropertyNames[x.DataModelPropertyName]).ToList();
        });
        _dataPropertyStoragePropertyNames = new Lazy<List<string>>(() =>
        {
            var storagePropertyNames = _storagePropertyNamesMap.Value;
            return _dataProperties.Select(x => storagePropertyNames[x.DataModelPropertyName]).ToList();
        });
        _vectorPropertyStoragePropertyNames = new Lazy<List<string>>(() =>
        {
            var storagePropertyNames = _storagePropertyNamesMap.Value;
            return _vectorProperties.Select(x => storagePropertyNames[x.DataModelPropertyName]).ToList();
        });
        _jsonPropertyNamesMap = new Lazy<Dictionary<string, string>>(() =>
        {
            return BuildPropertyNameToJsonPropertyNameMap((_keyProperties, _dataProperties, _vectorProperties),
                dataModelType, _options?.JsonSerializerOptions);
        });
        _keyPropertyJsonNames = new Lazy<List<string>>(() =>
        {
            var jsonPropertyNamesMap = _jsonPropertyNamesMap.Value;
            return _keyProperties.Select(x => jsonPropertyNamesMap[x.DataModelPropertyName]).ToList();
        });
        _dataPropertyJsonNames = new Lazy<List<string>>(() =>
        {
            var jsonPropertyNamesMap = _jsonPropertyNamesMap.Value;
            return _dataProperties.Select(x => jsonPropertyNamesMap[x.DataModelPropertyName]).ToList();
        });
        _vectorPropertyJsonNames = new Lazy<List<string>>(() =>
        {
            var jsonPropertyNamesMap = _jsonPropertyNamesMap.Value;
            return _vectorProperties.Select(x => jsonPropertyNamesMap[x.DataModelPropertyName]).ToList();
        });
    }
    /// <summary>Gets the record definition of the current storage model.</summary>
    public VectorStoreRecordDefinition RecordDefinition => _vectorStoreRecordDefinition;
    /// <summary>Gets the list of properties from the record definition.</summary>
    public IReadOnlyList<VectorStoreRecordProperty> Properties => _vectorStoreRecordDefinition.Properties;
    /// <summary>Gets the first <see cref="VectorStoreRecordKeyProperty"/> object from the record definition that was provided or that was generated from the data model.</summary>
    public VectorStoreRecordKeyProperty KeyProperty => _keyProperties[0];
    /// <summary>Gets all <see cref="VectorStoreRecordKeyProperty"/> objects from the record definition that was provided or that was generated from the data model.</summary>
    public IReadOnlyList<VectorStoreRecordKeyProperty> KeyProperties => _keyProperties;
    /// <summary>Gets all <see cref="VectorStoreRecordDataProperty"/> objects from the record definition that was provided or that was generated from the data model.</summary>
    public IReadOnlyList<VectorStoreRecordDataProperty> DataProperties => _dataProperties;
    /// <summary>Gets the first <see cref="VectorStoreRecordVectorProperty"/> objects from the record definition that was provided or that was generated from the data model.</summary>
    public VectorStoreRecordVectorProperty? VectorProperty => _vectorProperties.Count > 0 ? _vectorProperties[0] : null;
    /// <summary>Gets all <see cref="VectorStoreRecordVectorProperty"/> objects from the record definition that was provided or that was generated from the data model.</summary>
    public IReadOnlyList<VectorStoreRecordVectorProperty> VectorProperties => _vectorProperties;
    /// <summary>Gets the parameterless constructor if one exists, throws otherwise.</summary>
    public ConstructorInfo ParameterLessConstructorInfo => _parameterlessConstructorInfo.Value;
    /// <summary>Gets the first key property info object.</summary>
    public PropertyInfo KeyPropertyInfo { get { LoadPropertyInfoIfNeeded(); return _keyPropertiesInfo![0]; } }
    /// <summary>Gets the key property info objects.</summary>
    public IReadOnlyList<PropertyInfo> KeyPropertiesInfo { get { LoadPropertyInfoIfNeeded(); return _keyPropertiesInfo!; } }
    /// <summary>Gets the data property info objects.</summary>
    public IReadOnlyList<PropertyInfo> DataPropertiesInfo { get { LoadPropertyInfoIfNeeded(); return _dataPropertiesInfo!; } }
    /// <summary>Gets the vector property info objects.</summary>
    public IReadOnlyList<PropertyInfo> VectorPropertiesInfo { get { LoadPropertyInfoIfNeeded(); return _vectorPropertiesInfo!; } }
    /// <summary>Gets the name of the first vector property in the definition or null if there are no vectors.</summary>
    public string? FirstVectorPropertyName => _vectorProperties.FirstOrDefault()?.DataModelPropertyName;
    /// <summary>Gets the first vector PropertyInfo object in the data model or null if there are no vectors.</summary>
    public PropertyInfo? FirstVectorPropertyInfo => VectorPropertiesInfo.Count > 0 ? VectorPropertiesInfo[0] : null;
    /// <summary>Gets the property name of the first key property in the definition.</summary>
    public string KeyPropertyName => _keyProperties[0].DataModelPropertyName;
    /// <summary>Gets the storage name of the first key property in the definition.</summary>
    public string KeyPropertyStoragePropertyName => _keyPropertyStoragePropertyNames.Value[0];
    /// <summary>Gets the storage names of all the properties in the definition.</summary>
    public IReadOnlyDictionary<string, string> StoragePropertyNamesMap => _storagePropertyNamesMap.Value;
    /// <summary>Gets the storage names of the key properties in the definition.</summary>
    public IReadOnlyList<string> KeyPropertyStoragePropertyNames => _keyPropertyStoragePropertyNames.Value;
    /// <summary>Gets the storage names of the data properties in the definition.</summary>
    public IReadOnlyList<string> DataPropertyStoragePropertyNames => _dataPropertyStoragePropertyNames.Value;
    /// <summary>Gets the storage name of the first vector property in the definition or null if there are no vectors.</summary>
    public string? FirstVectorPropertyStoragePropertyName => FirstVectorPropertyName == null ? null : StoragePropertyNamesMap[FirstVectorPropertyName];
    /// <summary>Gets the storage names of the vector properties in the definition.</summary>
    public IReadOnlyList<string> VectorPropertyStoragePropertyNames => _vectorPropertyStoragePropertyNames.Value;
    /// <summary>Gets the json name of the first key property in the definition.</summary>
    public string KeyPropertyJsonName => KeyPropertyJsonNames[0];
    /// <summary>Gets the json names of the key properties in the definition.</summary>
    public IReadOnlyList<string> KeyPropertyJsonNames => _keyPropertyJsonNames.Value;
    /// <summary>Gets the json names of the data properties in the definition.</summary>
    public IReadOnlyList<string> DataPropertyJsonNames => _dataPropertyJsonNames.Value;
    /// <summary>Gets the json name of the first vector property in the definition or null if there are no vectors.</summary>
    public string? FirstVectorPropertyJsonName => FirstVectorPropertyName == null ? null : JsonPropertyNamesMap[FirstVectorPropertyName];
    /// <summary>Gets the json names of the vector properties in the definition.</summary>
    public IReadOnlyList<string> VectorPropertyJsonNames => _vectorPropertyJsonNames.Value;
    /// <summary>A map of data model property names to the names they will have if serialized to JSON.</summary>
    public IReadOnlyDictionary<string, string> JsonPropertyNamesMap => _jsonPropertyNamesMap.Value;
    /// <summary>Verify that the data model has a parameterless constructor.</summary>
    public void VerifyHasParameterlessConstructor() => _ = _parameterlessConstructorInfo.Value;
    /// <summary>Verify that the types of the key properties fall within the provided set.</summary>
    /// <param name="supportedTypes">The list of supported types.</param>
    public void VerifyKeyProperties(HashSet<Type> supportedTypes)
        => VectorStoreRecordPropertyVerification.VerifyPropertyTypes(_keyProperties, supportedTypes, "Key");
    /// <summary>Verify that the types of the data properties fall within the provided set.</summary>
    /// <param name="supportedTypes">The list of supported types.</param>
    /// <param name="supportEnumerable">A value indicating whether enumerable types are supported where the element type is one of the supported types.</param>
    public void VerifyDataProperties(HashSet<Type> supportedTypes, bool supportEnumerable)
        => VectorStoreRecordPropertyVerification.VerifyPropertyTypes(_dataProperties, supportedTypes, "Data", supportEnumerable);
    /// <summary>Verify that the types of the data properties fall within the provided set.</summary>
    /// <param name="supportedTypes">The list of supported types.</param>
    /// <param name="supportedEnumerableElementTypes">A value indicating whether enumerable types are supported where the element type is one of the supported types.</param>
    public void VerifyDataProperties(HashSet<Type> supportedTypes, HashSet<Type> supportedEnumerableElementTypes)
        => VectorStoreRecordPropertyVerification.VerifyPropertyTypes(_dataProperties, supportedTypes, supportedEnumerableElementTypes, "Data");
    /// <summary>Verify that the types of the vector properties fall within the provided set.</summary>
    /// <param name="supportedTypes">The list of supported types.</param>
    public void VerifyVectorProperties(HashSet<Type> supportedTypes)
        => VectorStoreRecordPropertyVerification.VerifyPropertyTypes(_vectorProperties, supportedTypes, "Vector");
    /// <summary>
    /// Get the storage property name for the given data model property name.
    /// </summary>
    /// <param name="dataModelPropertyName">The data model property name for which to get the storage property name.</param>
    /// <returns>The storage property name.</returns>
    public string GetStoragePropertyName(string dataModelPropertyName)
        => _storagePropertyNamesMap.Value[dataModelPropertyName];
    /// <summary>
    /// Get the name under which a property will be stored if serialized to JSON
    /// </summary>
    /// <param name="dataModelPropertyName">The data model property name for which to get the JSON name.</param>
    /// <returns>The JSON name.</returns>
    public string GetJsonPropertyName(string dataModelPropertyName)
        => _jsonPropertyNamesMap.Value[dataModelPropertyName];
    /// <summary>
    /// Get the vector property with the provided name if a name is provided, and fall back
    /// to a vector property in the schema if not. If no name is provided and there is more
    /// than one vector property, an exception will be thrown.
    /// </summary>
    /// <param name="searchOptions">The search options.</param>
    /// <exception cref="InvalidOperationException">Thrown if the provided property name is not a valid vector property name.</exception>
    public VectorStoreRecordVectorProperty GetVectorPropertyOrSingle<TRecord>(VectorSearchOptions<TRecord>? searchOptions)
    {
        if (searchOptions is not null)
        {
            string? vectorPropertyName = searchOptions.VectorPropertyName;
            // If vector property name is provided, try to find it in schema or throw an exception.
            if (!string.IsNullOrWhiteSpace(vectorPropertyName))
            {
                // Check vector properties by data model property name.
                return VectorProperties.FirstOrDefault(l => l.DataModelPropertyName.Equals(vectorPropertyName, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"The {_dataModelType.FullName} type does not have a vector property named '{vectorPropertyName}'.");
            }
            else if (searchOptions.VectorProperty is Expression<Func<TRecord, object?>> expression)
            {
                // VectorPropertiesInfo is not available for VectorStoreGenericDataModel.
                IReadOnlyList<PropertyInfo> infos = typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>) ? [] : VectorPropertiesInfo;
                return GetMatchingProperty<TRecord, VectorStoreRecordVectorProperty>(expression, infos, VectorProperties);
            }
        }
        // If vector property name is not provided, check if there is a single vector property, or throw if there are no vectors or more than one.
        if (VectorProperty is null)
            throw new InvalidOperationException($"The {_dataModelType.FullName} type does not have any vector properties.");
        if (VectorProperties.Count > 1)
            throw new InvalidOperationException($"The {_dataModelType.FullName} type has multiple vector properties, please specify your chosen property via options.");
        return VectorProperty;
    }
    /// <summary>
    /// Get the text data property, that has full text search indexing enabled, with the provided name if a name is provided, and fall back
    /// to a text data property in the schema if not. If no name is provided and there is more than one text data property with
    /// full text search indexing enabled, an exception will be thrown.
    /// </summary>
    /// <param name="expression">The full text search property selector.</param>
    /// <exception cref="InvalidOperationException">Thrown if the provided property name is not a valid text data property name.</exception>
    public VectorStoreRecordDataProperty GetFullTextDataPropertyOrSingle<TRecord>(Expression<Func<TRecord, object?>>? expression)
    {
        if (expression is not null)
        {
            // DataPropertiesInfo is not available for VectorStoreGenericDataModel.
            IReadOnlyList<PropertyInfo> infos = typeof(TRecord).IsGenericType && typeof(TRecord).GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>) ? [] : DataPropertiesInfo;
            var dataProperty = GetMatchingProperty<TRecord, VectorStoreRecordDataProperty>(expression, DataPropertiesInfo, DataProperties);
            return dataProperty.IsFullTextSearchable ? dataProperty
                : throw new InvalidOperationException($"The text data property named '{dataProperty.DataModelPropertyName}' on the {_dataModelType.FullName} type must have full text search enabled.");
        }
        // If text data property name is not provided, check if a single full text searchable text property exists or throw otherwise.
        var fullTextStringProperties = DataProperties
            .Where(l => l.PropertyType == typeof(string) && l.IsFullTextSearchable)
            .ToList();
        if (fullTextStringProperties.Count == 0)
            throw new InvalidOperationException($"The {_dataModelType.FullName} type does not have any text data properties that have full text search enabled.");
        if (fullTextStringProperties.Count > 1)
            throw new InvalidOperationException($"The {_dataModelType.FullName} type has multiple text data properties that have full text search enabled, please specify your chosen property via options.");
        return fullTextStringProperties[0];
    }
    private static TProperty GetMatchingProperty<TRecord, TProperty>(Expression<Func<TRecord, object?>> expression,
        IReadOnlyList<PropertyInfo> propertyInfos, IReadOnlyList<TProperty> properties) where TProperty : VectorStoreRecordProperty
    {
        bool data = typeof(TProperty) == typeof(VectorStoreRecordDataProperty);
        string expectedGenericModelPropertyName = data ? nameof(VectorStoreGenericDataModel<object>.Data) : nameof(VectorStoreGenericDataModel<object>.Vectors);
        MemberExpression? member = expression.Body as MemberExpression;
        // (TRecord r) => r.PropertyName is translated into
        // (TRecord r) => (object)r.PropertyName for properties that return struct like ReadOnlyMemory<float>.
        if (member is null && expression.Body is UnaryExpression unary && unary.Operand.NodeType == ExpressionType.MemberAccess)
            member = unary.Operand as MemberExpression;
        if (member is not null && expression.Parameters.Count == 1
            && member.Expression == expression.Parameters[0] && member.Member is PropertyInfo property)
        {
            for (int i = 0; i < propertyInfos.Count; i++)
            {
                if (propertyInfos[i] == property)
                    return properties[i];
            }
            throw new InvalidOperationException($"The property {property.Name} of {typeof(TRecord).FullName} is not a {(data ? "Data" : "Vector")} property.");
        }
        // (VectorStoreGenericDataModel r) => r.Vectors["PropertyName"]
        else if (expression.Body is MethodCallExpression methodCall
            // It's a Func<VectorStoreGenericDataModel<TKey>, object>
            && expression.Type.IsGenericType
            && expression.Type.GenericTypeArguments.Length == 2
            && expression.Type.GenericTypeArguments[0].IsGenericType
            && expression.Type.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(VectorStoreGenericDataModel<>)
            // It's accessing VectorStoreGenericDataModel.Vectors (or Data)
            && methodCall.Object is MemberExpression memberAccess
            && memberAccess.Member.Name == expectedGenericModelPropertyName
            // and has a single argument
            && methodCall.Arguments.Count == 1)
        {
            string name = methodCall.Arguments[0] switch
            {
                ConstantExpression constant when constant.Value is string text => text,
                MemberExpression field when TryGetCapturedValue(field, out object? capturedValue) && capturedValue is string text => text,
                _ => throw new InvalidOperationException($"The value of the provided {(data ? "Additional" : "Vector")}Property option is not a valid expression.")
            };
            return properties.FirstOrDefault(l => l.DataModelPropertyName.Equals(name, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"The {typeof(TRecord).FullName} type does not have a vector property named '{name}'.");
        }
        throw new InvalidOperationException($"The value of the provided {(data ? "Additional" : "Vector")}Property option is not a valid expression.");
        static bool TryGetCapturedValue(Expression expression, out object? capturedValue)
        {
            if (expression is MemberExpression { Expression: ConstantExpression constant, Member: FieldInfo fieldInfo }
                && constant.Type.Attributes.HasFlag(TypeAttributes.NestedPrivate)
                && Attribute.IsDefined(constant.Type, typeof(CompilerGeneratedAttribute), inherit: true))
            {
                capturedValue = fieldInfo.GetValue(constant.Value);
                return true;
            }
            capturedValue = null;
            return false;
        }
    }
    /// <summary>
    /// Check if we have previously loaded the <see cref="PropertyInfo"/> objects from the data model and if not, load them.
    /// </summary>
    private void LoadPropertyInfoIfNeeded()
    {
        if (_keyPropertiesInfo != null) return;
        // If we previously built the definition from the data model, the PropertyInfo objects
        // from the data model would already be saved. If we didn't though, there could be a mismatch
        // between what is defined in the definition and what is in the data model. Therefore, this
        // method will throw if any property in the definition is not on the data model.
        var propertiesInfo = FindPropertiesInfo(_dataModelType, _vectorStoreRecordDefinition);
        _keyPropertiesInfo = propertiesInfo.KeyProperties;
        _dataPropertiesInfo = propertiesInfo.DataProperties;
        _vectorPropertiesInfo = propertiesInfo.VectorProperties;
    }
    /// <summary>
    /// Split the given <paramref name="definition"/> into key, data and vector properties and verify that we have the expected numbers of each type.
    /// </summary>
    /// <param name="typeName">The name of the type that the definition relates to.</param>
    /// <param name="definition">The <see cref="VectorStoreRecordDefinition"/> to split.</param>
    /// <param name="supportsMultipleKeys">A value indicating whether multiple key properties are supported.</param>
    /// <param name="supportsMultipleVectors">A value indicating whether multiple vectors are supported.</param>
    /// <param name="requiresAtLeastOneVector">A value indicating whether we need at least one vector.</param>
    /// <returns>The properties on the <see cref="VectorStoreRecordDefinition"/> split into key, data and vector groupings.</returns>
    /// <exception cref="ArgumentException">Thrown if there are any validation failures with the provided <paramref name="definition"/>.</exception>
    private static (List<VectorStoreRecordKeyProperty> KeyProperties, List<VectorStoreRecordDataProperty> DataProperties, List<VectorStoreRecordVectorProperty> VectorProperties) SplitDefinitionAndVerify(
        string typeName, VectorStoreRecordDefinition definition, bool supportsMultipleKeys, bool supportsMultipleVectors, bool requiresAtLeastOneVector)
    {
        var keyProperties = definition.Properties.OfType<VectorStoreRecordKeyProperty>().ToList();
        var dataProperties = definition.Properties.OfType<VectorStoreRecordDataProperty>().ToList();
        var vectorProperties = definition.Properties.OfType<VectorStoreRecordVectorProperty>().ToList();
        if (keyProperties.Count > 1 && !supportsMultipleKeys)
            throw new ArgumentException($"Multiple key properties found on type {typeName} or the provided {nameof(VectorStoreRecordDefinition)}.");
        if (keyProperties.Count == 0)
            throw new ArgumentException($"No key property found on type {typeName} or the provided {nameof(VectorStoreRecordDefinition)}.");
        if (requiresAtLeastOneVector && vectorProperties.Count == 0)
            throw new ArgumentException($"No vector property found on type {typeName} or the provided {nameof(VectorStoreRecordDefinition)} while at least one is required.");
        if (!supportsMultipleVectors && vectorProperties.Count > 1)
            throw new ArgumentException($"Multiple vector properties found on type {typeName} or the provided {nameof(VectorStoreRecordDefinition)} while only one is supported.");
        return (keyProperties, dataProperties, vectorProperties);
    }
    /// <summary>
    /// Find the properties with <see cref="VectorStoreRecordKeyAttribute"/>, <see cref="VectorStoreRecordDataAttribute"/> and <see cref="VectorStoreRecordVectorAttribute"/> attributes
    /// and verify that they exist and that we have the expected numbers of each type.
    /// Return those properties in separate categories.
    /// </summary>
    /// <param name="type">The data model to find the properties on.</param>
    /// <returns>The categorized properties.</returns>
    private static (List<PropertyInfo> KeyProperties, List<PropertyInfo> DataProperties, List<PropertyInfo> VectorProperties) FindPropertiesInfo([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        List<PropertyInfo> keyProperties = new();
        List<PropertyInfo> dataProperties = new();
        List<PropertyInfo> vectorProperties = new();
        foreach (var property in type.GetProperties())
        {
            // Get Key property.
            if (property.GetCustomAttribute<VectorStoreRecordKeyAttribute>() is not null)
                keyProperties.Add(property);
            // Get data properties.
            if (property.GetCustomAttribute<VectorStoreRecordDataAttribute>() is not null)
                dataProperties.Add(property);
            // Get Vector properties.
            if (property.GetCustomAttribute<VectorStoreRecordVectorAttribute>() is not null)
                vectorProperties.Add(property);
        }
        return (keyProperties, dataProperties, vectorProperties);
    }
    /// <summary>
    /// Find the properties listed in the <paramref name="vectorStoreRecordDefinition"/> on the <paramref name="type"/> and verify
    /// that they exist.
    /// Return those properties in separate categories.
    /// </summary>
    /// <param name="type">The data model to find the properties on.</param>
    /// <param name="vectorStoreRecordDefinition">The property configuration.</param>
    /// <returns>The categorized properties.</returns>
    public static (List<PropertyInfo> KeyProperties, List<PropertyInfo> DataProperties, List<PropertyInfo> VectorProperties) FindPropertiesInfo([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, VectorStoreRecordDefinition vectorStoreRecordDefinition)
    {
        List<PropertyInfo> keyProperties = new();
        List<PropertyInfo> dataProperties = new();
        List<PropertyInfo> vectorProperties = new();
        foreach (VectorStoreRecordProperty property in vectorStoreRecordDefinition.Properties)
        {
            // Key.
            if (property is VectorStoreRecordKeyProperty keyPropertyInfo)
            {
                var keyProperty = type.GetProperty(keyPropertyInfo.DataModelPropertyName);
                if (keyProperty == null)
                    throw new ArgumentException($"Key property '{keyPropertyInfo.DataModelPropertyName}' not found on type {type.FullName}.");
                keyProperties.Add(keyProperty);
            }
            // Data.
            else if (property is VectorStoreRecordDataProperty dataPropertyInfo)
            {
                var dataProperty = type.GetProperty(dataPropertyInfo.DataModelPropertyName);
                if (dataProperty == null)
                    throw new ArgumentException($"Data property '{dataPropertyInfo.DataModelPropertyName}' not found on type {type.FullName}.");
                dataProperties.Add(dataProperty);
            }
            // Vector.
            else if (property is VectorStoreRecordVectorProperty vectorPropertyInfo)
            {
                var vectorProperty = type.GetProperty(vectorPropertyInfo.DataModelPropertyName);
                if (vectorProperty == null)
                    throw new ArgumentException($"Vector property '{vectorPropertyInfo.DataModelPropertyName}' not found on type {type.FullName}.");
                vectorProperties.Add(vectorProperty);
            }
            else
                throw new ArgumentException($"Unknown property type '{property.GetType().FullName}' in vector store record definition.");
        }
        return (keyProperties, dataProperties, vectorProperties);
    }
    /// <summary>
    /// Create a <see cref="VectorStoreRecordDefinition"/> by reading the attributes on the provided <see cref="PropertyInfo"/> objects.
    /// </summary>
    /// <param name="propertiesInfo"><see cref="PropertyInfo"/> objects to build a <see cref="VectorStoreRecordDefinition"/> from.</param>
    /// <returns>The <see cref="VectorStoreRecordDefinition"/> based on the given <see cref="PropertyInfo"/> objects.</returns>
    private static VectorStoreRecordDefinition CreateVectorStoreRecordDefinitionFromType((List<PropertyInfo> KeyProperties, List<PropertyInfo> DataProperties, List<PropertyInfo> VectorProperties) propertiesInfo)
    {
        var definitionProperties = new List<VectorStoreRecordProperty>();
        // Key properties.
        foreach (var keyProperty in propertiesInfo.KeyProperties)
        {
            var keyAttribute = keyProperty.GetCustomAttribute<VectorStoreRecordKeyAttribute>();
            if (keyAttribute is not null)
            {
                definitionProperties.Add(new VectorStoreRecordKeyProperty(keyProperty.Name, keyProperty.PropertyType)
                {
                    StoragePropertyName = keyAttribute.StoragePropertyName
                });
            }
        }
        // Data properties.
        foreach (var dataProperty in propertiesInfo.DataProperties)
        {
            var dataAttribute = dataProperty.GetCustomAttribute<VectorStoreRecordDataAttribute>();
            if (dataAttribute is not null)
            {
                definitionProperties.Add(new VectorStoreRecordDataProperty(dataProperty.Name, dataProperty.PropertyType)
                {
                    IsFilterable = dataAttribute.IsFilterable,
                    IsFullTextSearchable = dataAttribute.IsFullTextSearchable,
                    StoragePropertyName = dataAttribute.StoragePropertyName
                });
            }
        }
        // Vector properties.
        foreach (var vectorProperty in propertiesInfo.VectorProperties)
        {
            var vectorAttribute = vectorProperty.GetCustomAttribute<VectorStoreRecordVectorAttribute>();
            if (vectorAttribute is not null)
            {
                definitionProperties.Add(new VectorStoreRecordVectorProperty(vectorProperty.Name, vectorProperty.PropertyType)
                {
                    Dimensions = vectorAttribute.Dimensions,
                    IndexKind = vectorAttribute.IndexKind,
                    DistanceFunction = vectorAttribute.DistanceFunction,
                    StoragePropertyName = vectorAttribute.StoragePropertyName
                });
            }
        }
        return new VectorStoreRecordDefinition { Properties = definitionProperties };
    }
    /// <summary>
    /// Build a map of property names to the names under which they should be saved in storage, for the given properties.
    /// </summary>
    /// <param name="properties">The properties to build the map for.</param>
    /// <returns>The map from property names to the names under which they should be saved in storage.</returns>
    private static Dictionary<string, string> BuildPropertyNameToStorageNameMap((List<VectorStoreRecordKeyProperty> keyProperties, List<VectorStoreRecordDataProperty> dataProperties, List<VectorStoreRecordVectorProperty> vectorProperties) properties)
    {
        var storagePropertyNameMap = new Dictionary<string, string>();
        foreach (var keyProperty in properties.keyProperties)
            storagePropertyNameMap.Add(keyProperty.DataModelPropertyName, keyProperty.StoragePropertyName ?? keyProperty.DataModelPropertyName);
        foreach (var dataProperty in properties.dataProperties)
            storagePropertyNameMap.Add(dataProperty.DataModelPropertyName, dataProperty.StoragePropertyName ?? dataProperty.DataModelPropertyName);
        foreach (var vectorProperty in properties.vectorProperties)
            storagePropertyNameMap.Add(vectorProperty.DataModelPropertyName, vectorProperty.StoragePropertyName ?? vectorProperty.DataModelPropertyName);
        return storagePropertyNameMap;
    }
    /// <summary>
    /// Build a map of property names to the names that they would have if serialized to JSON.
    /// </summary>
    /// <param name="properties">The properties to build the map for.</param>
    /// <param name="dataModel">The data model type that the property belongs to.</param>
    /// <param name="options">The options used for JSON serialization.</param>
    /// <returns>The map from property names to the names that they would have if serialized to JSON.</returns>
    private static Dictionary<string, string> BuildPropertyNameToJsonPropertyNameMap(
        (List<VectorStoreRecordKeyProperty> keyProperties, List<VectorStoreRecordDataProperty> dataProperties, List<VectorStoreRecordVectorProperty> vectorProperties) properties,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type dataModel,
        JsonSerializerOptions? options)
    {
        var jsonPropertyNameMap = new Dictionary<string, string>();
        foreach (var keyProperty in properties.keyProperties)
            jsonPropertyNameMap.Add(keyProperty.DataModelPropertyName, GetJsonPropertyName(keyProperty, dataModel, options));
        foreach (var dataProperty in properties.dataProperties)
            jsonPropertyNameMap.Add(dataProperty.DataModelPropertyName, GetJsonPropertyName(dataProperty, dataModel, options));
        foreach (var vectorProperty in properties.vectorProperties)
            jsonPropertyNameMap.Add(vectorProperty.DataModelPropertyName, GetJsonPropertyName(vectorProperty, dataModel, options));
        return jsonPropertyNameMap;
    }
    /// <summary>
    /// Get the JSON property name of a property by using the <see cref="JsonPropertyNameAttribute"/> if available, otherwise
    /// using the <see cref="JsonNamingPolicy"/> if available, otherwise falling back to the property name.
    /// The provided <paramref name="dataModel"/> may not actually contain the property, e.g. when the user has a data model that
    /// doesn't resemble the stored data and where they are using a custom mapper.
    /// </summary>
    /// <param name="property">The property to retrieve a JSON name for.</param>
    /// <param name="dataModel">The data model type that the property belongs to.</param>
    /// <param name="options">The options used for JSON serialization.</param>
    /// <returns>The JSON property name.</returns>
    private static string GetJsonPropertyName(VectorStoreRecordProperty property, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type dataModel, JsonSerializerOptions? options)
    {
        var propertyInfo = dataModel.GetProperty(property.DataModelPropertyName);
        if (propertyInfo != null)
        {
            var jsonPropertyNameAttribute = propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (jsonPropertyNameAttribute is not null)
                return jsonPropertyNameAttribute.Name;
        }
        if (options?.PropertyNamingPolicy is not null)
            return options.PropertyNamingPolicy.ConvertName(property.DataModelPropertyName);
        return property.DataModelPropertyName;
    }
}
[ExcludeFromCodeCoverage]
internal sealed class VectorStoreRecordPropertyReaderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the connector/db supports multiple key properties.
    /// </summary>
    public bool SupportsMultipleKeys { get; set; } = false;
    /// <summary>
    /// Gets or sets a value indicating whether the connector/db supports multiple vector properties.
    /// </summary>
    public bool SupportsMultipleVectors { get; set; } = true;
    /// <summary>
    /// Gets or sets a value indicating whether the connector/db requires at least one vector property.
    /// </summary>
    public bool RequiresAtLeastOneVector { get; set; } = false;
    /// <summary>
    /// Gets or sets the json serializer options that the connector might be using for storage serialization.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
internal static class InMemoryVectorStoreCollectionSearchMapping
{
    /// <summary>
    /// Compare the two vectors using the specified distance function.
    /// </summary>
    /// <param name="x">The first vector to compare.</param>
    /// <param name="y">The second vector to compare.</param>
    /// <param name="distanceFunction">The distance function to use for comparison.</param>
    /// <returns>The score of the comparison.</returns>
    /// <exception cref="NotSupportedException">Thrown when the distance function is not supported.</exception>
    public static float CompareVectors(ReadOnlySpan<float> x, ReadOnlySpan<float> y, string? distanceFunction)
    {
        switch (distanceFunction)
        {
            case null:
            case DistanceFunction.CosineSimilarity:
            case DistanceFunction.CosineDistance: return TensorPrimitives.CosineSimilarity(x, y);
            case DistanceFunction.DotProductSimilarity: return TensorPrimitives.Dot(x, y);
            case DistanceFunction.EuclideanDistance: return TensorPrimitives.Distance(x, y);
            default: throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported by the InMemory connector.");
        }
    }
    /// <summary>
    /// Indicates whether result ordering should be descending or ascending, to get most similar results at the top, based on the distance function.
    /// </summary>
    /// <param name="distanceFunction">The distance function to use for comparison.</param>
    /// <returns>Whether to order descending or ascending.</returns>
    /// <exception cref="NotSupportedException">Thrown when the distance function is not supported.</exception>
    public static bool ShouldSortDescending(string? distanceFunction)
    {
        switch (distanceFunction)
        {
            case null:
            case DistanceFunction.CosineSimilarity:
            case DistanceFunction.DotProductSimilarity: return true;
            case DistanceFunction.CosineDistance:
            case DistanceFunction.EuclideanDistance: return false;
            default: throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported by the InMemory connector.");
        }
    }
    /// <summary>
    /// Converts the provided score into the correct result depending on the distance function.
    /// The main purpose here is to convert from cosine similarity to cosine distance if cosine distance is requested,
    /// since the two are inversely related and the <see cref="TensorPrimitives"/> only supports cosine similarity so
    /// we are using cosine similarity for both similarity and distance.
    /// </summary>
    /// <param name="score">The score to convert.</param>
    /// <param name="distanceFunction">The distance function to use for comparison.</param>
    /// <returns>Whether to order descending or ascending.</returns>
    /// <exception cref="NotSupportedException">Thrown when the distance function is not supported.</exception>
    public static float ConvertScore(float score, string? distanceFunction)
    {
        switch (distanceFunction)
        {
            case DistanceFunction.CosineDistance: return 1 - score;
            case null:
            case DistanceFunction.CosineSimilarity:
            case DistanceFunction.DotProductSimilarity:
            case DistanceFunction.EuclideanDistance: return score;
            default: throw new NotSupportedException($"The distance function '{distanceFunction}' is not supported by the InMemory connector.");
        }
    }
    /// <summary>
    /// Filter the provided records using the provided filter definition.
    /// </summary>
    /// <param name="filter">The filter definition to filter the <paramref name="records"/> with.</param>
    /// <param name="records">The records to filter.</param>
    /// <returns>The filtered records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported filter clause is encountered.</exception>
    public static IEnumerable<TRecord> FilterRecords<TRecord>(VectorSearchFilter filter, IEnumerable<TRecord> records)
    {
        return records.Where(record =>
        {
            if (record is null) return false;
            var result = true;
            // Run each filter clause against the record, and AND the results together.
            // Break if any clause returns false, since we are doing an AND and no need
            // to check any further clauses.
            foreach (var clause in filter.FilterClauses)
            {
                if (clause is EqualToFilterClause equalToFilter)
                {
                    result = result && CheckEqualTo(record, equalToFilter);
                    if (result == false) break;
                }
                else if (clause is AnyTagEqualToFilterClause anyTagEqualToFilter)
                {
                    result = result && CheckAnyTagEqualTo(record, anyTagEqualToFilter);
                    if (result == false) break;
                }
                else
                    throw new InvalidOperationException($"Unsupported filter clause type {clause.GetType().Name}");
            }
            return result;
        });
    }
    /// <summary>
    /// Check if the required property on the record is equal to the required value form the filter.
    /// </summary>
    /// <param name="record">The record to check against the filter.</param>
    /// <param name="equalToFilter">The filter containing the property and value to check.</param>
    /// <returns><see langword="true"/> if the property equals the required value, <see langword="false"/> otherwise.</returns>
    private static bool CheckEqualTo(object record, EqualToFilterClause equalToFilter)
    {
        var propertyInfo = GetPropertyInfo(record, equalToFilter.FieldName);
        var propertyValue = propertyInfo.GetValue(record);
        if (propertyValue == null)
            return propertyValue == equalToFilter.Value;
        return propertyValue.Equals(equalToFilter.Value);
    }
    /// <summary>
    /// Check if the required tag list on the record is equal to the required value form the filter.
    /// </summary>
    /// <param name="record">The record to check against the filter.</param>
    /// <param name="anyTagEqualToFilter">The filter containing the property and value to check.</param>
    /// <returns><see langword="true"/> if the tag list contains the required value, <see langword="false"/> otherwise.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static bool CheckAnyTagEqualTo(object record, AnyTagEqualToFilterClause anyTagEqualToFilter)
    {
        var propertyInfo = GetPropertyInfo(record, anyTagEqualToFilter.FieldName);
        // Check that the property is actually a list of values.
        if (!typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
            throw new InvalidOperationException($"Property {anyTagEqualToFilter.FieldName} is not a list property on record type {record.GetType().Name}");
        // Check that the tag list contains any values. If not, return false, since the required value cannot be in an empty list.
        var propertyValue = propertyInfo.GetValue(record) as IEnumerable;
        if (propertyValue == null) return false;
        // Check each value in the tag list against the required value.
        foreach (var value in propertyValue)
        {
            if (value == null && anyTagEqualToFilter.Value == null) return true;
            if (value != null && value.Equals(anyTagEqualToFilter.Value)) return true;
        }
        return false;
    }
    /// <summary>
    /// Get the property info for the provided property name on the record.
    /// </summary>
    /// <param name="record">The record to find the property on.</param>
    /// <param name="propertyName">The name of the property to find.</param>
    /// <returns>The property info for the required property.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the required property does not exist on the record.</exception>
    private static PropertyInfo GetPropertyInfo(object record, string propertyName)
    {
        var propertyInfo = record.GetType().GetProperty(propertyName);
        if (propertyInfo == null)
            throw new InvalidOperationException($"Property {propertyName} not found on record type {record.GetType().Name}");
        return propertyInfo;
    }
}
internal static class AsyncEnumerable
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
            yield return item;
    }
}