using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Jobs;

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// An unordered, expandable set of unique values.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerTypeProxy(typeof(UnsafeHashSetDebuggerTypeProxy<>))]
    [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
    public unsafe struct UnsafeParallelHashSet<T>
        : INativeDisposable
        , IEnumerable<T>  // Used by collection initializers.
        where T : unmanaged, IEquatable<T>
    {
        internal UnsafeParallelHashMap<T, bool> m_Data;

        /// <summary>
        /// Initializes and returns an instance of UnsafeHashSet.
        /// </summary>
        /// <param name="capacity">The number of values that should fit in the initial allocation.</param>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeParallelHashSet(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_Data = new UnsafeParallelHashMap<T, bool>(capacity, allocator);
        }

        /// <summary>
        /// Whether this set is empty.
        /// </summary>
        /// <value>True if this set is empty.</value>
        public bool IsEmpty => m_Data.IsEmpty;

        /// <summary>
        /// Returns the current number of values in this set.
        /// </summary>
        /// <returns>The current number of values in this set.</returns>
        public int Count() => m_Data.Count();

        /// <summary>
        /// The number of values that fit in the current allocation.
        /// </summary>
        /// <value>The number of values that fit in the current allocation.</value>
        /// <param name="value">A new capacity. Must be larger than current capacity.</param>
        /// <exception cref="Exception">Thrown if `value` is less than the current capacity.</exception>
        public int Capacity { get => m_Data.Capacity; set => m_Data.Capacity = value; }

        /// <summary>
        /// Whether this set has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this set has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_Data.IsCreated;

        /// <summary>
        /// Releases all resources (memory).
        /// </summary>
        public void Dispose() => m_Data.Dispose();

        /// <summary>
        /// Creates and schedules a job that will dispose this set.
        /// </summary>
        /// <param name="inputDeps">A job handle. The newly scheduled job will depend upon this handle.</param>
        /// <returns>The handle of a new job that will dispose this set.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Dispose(JobHandle inputDeps) => m_Data.Dispose(inputDeps);

        /// <summary>
        /// Removes all values.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear() => m_Data.Clear();

        /// <summary>
        /// Adds a new value (unless it is already present).
        /// </summary>
        /// <param name="item">The value to add.</param>
        /// <returns>True if the value was not already present.</returns>
        public bool Add(T item) => m_Data.TryAdd(item, false);

        /// <summary>
        /// Removes a particular value.
        /// </summary>
        /// <param name="item">The value to remove.</param>
        /// <returns>True if the value was present.</returns>
        public bool Remove(T item) => m_Data.Remove(item);

        /// <summary>
        /// Returns true if a particular value is present.
        /// </summary>
        /// <param name="item">The value to check for.</param>
        /// <returns>True if the value was present.</returns>
        public bool Contains(T item) => m_Data.ContainsKey(item);

        /// <summary>
        /// Returns an array with a copy of this set's values (in no particular order).
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array with a copy of the set's values.</returns>
        public NativeArray<T> ToNativeArray(AllocatorManager.AllocatorHandle allocator) => m_Data.GetKeyArray(allocator);

        /// <summary>
        /// Returns a parallel writer.
        /// </summary>
        /// <returns>A parallel writer.</returns>
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter { m_Data = m_Data.AsParallelWriter() };
        }

        /// <summary>
        /// A parallel writer for an UnsafeHashSet.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a set.
        /// </remarks>
        [NativeContainerIsAtomicWriteOnly]
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public struct ParallelWriter
        {
            internal UnsafeParallelHashMap<T, bool>.ParallelWriter m_Data;

            /// <summary>
            /// The number of values that fit in the current allocation.
            /// </summary>
            /// <value>The number of values that fit in the current allocation.</value>
            public int Capacity => m_Data.Capacity;

            /// <summary>
            /// Adds a new value (unless it is already present).
            /// </summary>
            /// <param name="item">The value to add.</param>
            /// <returns>True if the value is not already present.</returns>
            public bool Add(T item) => m_Data.TryAdd(item, false);
        }

        /// <summary>
        /// Returns an enumerator over the values of this set.
        /// </summary>
        /// <returns>An enumerator over the values of this set.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator { m_Enumerator = new UnsafeParallelHashMapDataEnumerator(m_Data.m_Buffer) };
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
        /// </summary>
        /// <returns>Throws NotImplementedException.</returns>
        /// <exception cref="NotImplementedException">Method is not implemented.</exception>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// An enumerator over the values of a set.
        /// </summary>
        /// <remarks>
        /// In an enumerator's initial state, <see cref="Current"/> is invalid.
        /// The first <see cref="MoveNext"/> call advances the enumerator to the first value.
        /// </remarks>
        public struct Enumerator : IEnumerator<T>
        {
            internal UnsafeParallelHashMapDataEnumerator m_Enumerator;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next value.
            /// </summary>
            /// <returns>True if `Current` is valid to read after the call.</returns>
            public bool MoveNext() => m_Enumerator.MoveNext();

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => m_Enumerator.Reset();

            /// <summary>
            /// The current value.
            /// </summary>
            /// <value>The current value.</value>
            public T Current => m_Enumerator.GetCurrentKey<T>();

            object IEnumerator.Current => Current;
        }
    }

    sealed internal class UnsafeHashSetDebuggerTypeProxy<T>
        where T : unmanaged, IEquatable<T>
    {
#if !NET_DOTS
        UnsafeParallelHashSet<T> Data;

        public UnsafeHashSetDebuggerTypeProxy(UnsafeParallelHashSet<T> data)
        {
            Data = data;
        }

        public List<T> Items
        {
            get
            {
                var result = new List<T>();
                using (var item = Data.ToNativeArray(Allocator.Temp))
                {
                    for (var k = 0; k < item.Length; ++k)
                    {
                        result.Add(item[k]);
                    }
                }

                return result;
            }
        }
#endif
    }
}
