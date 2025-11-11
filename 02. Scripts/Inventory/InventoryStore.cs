using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;

    public class InventoryStore : MonoBehaviour
    {
        readonly Dictionary<string, int> counts = new();
        public event Action<string, int> OnInventoryChanged;
        public int GetCount(string key) => counts.TryGetValue(key, out var n) ? n : 0;

        public bool Add(string key, int amount = 1)
        {
            if (string.IsNullOrEmpty(key) || amount <= 0) return false;
            counts.TryGetValue(key, out var cur);
            counts[key] = cur + amount;
            return true;
        }

        public bool Remove(string key, int amount = 1)
        {
            if (!counts.TryGetValue(key, out var cur) || amount <= 0) return false;
            var next = cur - amount;
            if (next <= 0) counts.Remove(key);
            else counts[key] = next;
            return true;
        }

        public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(counts);
    }