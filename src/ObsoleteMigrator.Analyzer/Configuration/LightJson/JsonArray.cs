#nullable disable

// ReSharper disable once CheckNamespace
namespace LightJson;

using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Represents an ordered collection of JsonValues.
/// </summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(JsonArrayDebugView))]
internal sealed class JsonArray : IEnumerable<JsonValue>
{
    private readonly IList<JsonValue> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArray"/> class.
    /// </summary>
    public JsonArray()
    {
        _items = new List<JsonValue>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArray"/> class, adding the given values to the collection.
    /// </summary>
    /// <param name="values">The values to be added to this collection.</param>
    public JsonArray(params JsonValue[] values)
        : this()
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        foreach (var value in values)
        {
            _items.Add(value);
        }
    }

    /// <summary>
    /// Gets the number of values in this collection.
    /// </summary>
    /// <value>The number of values in this collection.</value>
    public int Count
    {
        get
        {
            return _items.Count;
        }
    }

    /// <summary>
    /// Gets or sets the value at the given index.
    /// </summary>
    /// <param name="index">The zero-based index of the value to get or set.</param>
    /// <remarks>
    /// <para>The getter will return JsonValue.Null if the given index is out of range.</para>
    /// </remarks>
    public JsonValue this[int index]
    {
        get
        {
            if (index >= 0 && index < _items.Count)
            {
                return _items[index];
            }
            else
            {
                return JsonValue.Null;
            }
        }

        set
        {
            _items[index] = value;
        }
    }

    /// <summary>
    /// Adds the given value to this collection.
    /// </summary>
    /// <param name="value">The value to be added.</param>
    /// <returns>Returns this collection.</returns>
    public JsonArray Add(JsonValue value)
    {
        _items.Add(value);
        return this;
    }

    /// <summary>
    /// Inserts the given value at the given index in this collection.
    /// </summary>
    /// <param name="index">The index where the given value will be inserted.</param>
    /// <param name="value">The value to be inserted into this collection.</param>
    /// <returns>Returns this collection.</returns>
    public JsonArray Insert(int index, JsonValue value)
    {
        _items.Insert(index, value);
        return this;
    }

    /// <summary>
    /// Removes the value at the given index.
    /// </summary>
    /// <param name="index">The index of the value to be removed.</param>
    /// <returns>Return this collection.</returns>
    public JsonArray Remove(int index)
    {
        _items.RemoveAt(index);
        return this;
    }

    /// <summary>
    /// Clears the contents of this collection.
    /// </summary>
    /// <returns>Returns this collection.</returns>
    public JsonArray Clear()
    {
        _items.Clear();
        return this;
    }

    /// <summary>
    /// Determines whether the given item is in the JsonArray.
    /// </summary>
    /// <param name="item">The item to locate in the JsonArray.</param>
    /// <returns>Returns true if the item is found; otherwise, false.</returns>
    public bool Contains(JsonValue item)
    {
        return _items.Contains(item);
    }

    /// <summary>
    /// Determines the index of the given item in this JsonArray.
    /// </summary>
    /// <param name="item">The item to locate in this JsonArray.</param>
    /// <returns>The index of the item, if found. Otherwise, returns -1.</returns>
    public int IndexOf(JsonValue item)
    {
        return _items.IndexOf(item);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator that iterates through the collection.</returns>
    public IEnumerator<JsonValue> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>The enumerator that iterates through the collection.</returns>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private class JsonArrayDebugView
    {
        private readonly JsonArray _jsonArray;

        public JsonArrayDebugView(JsonArray jsonArray)
        {
            _jsonArray = jsonArray;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public JsonValue[] Items
        {
            get
            {
                var items = new JsonValue[_jsonArray.Count];

                for (int i = 0; i < _jsonArray.Count; i += 1)
                {
                    items[i] = _jsonArray[i];
                }

                return items;
            }
        }
    }
}