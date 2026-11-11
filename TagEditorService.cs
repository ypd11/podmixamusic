
using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsShuffle.Classes
{
    public class TagEditorService
    {
        private readonly Dictionary<string, PendingMetadata> _pendingMetadata =
            new Dictionary<string, PendingMetadata>(StringComparer.OrdinalIgnoreCase);

        private readonly object _syncRoot = new object();

        // Store metadata for a specific track.
        public void StageMetadata(string filePath, PendingMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A file path is required.", "filePath");

            if (metadata == null)
                throw new ArgumentNullException("metadata");

            string normalizedPath = NormalizePath(filePath);

            lock (_syncRoot)
            {
                _pendingMetadata[normalizedPath] = metadata;
            }
        }

        // Retrieve pending metadata for a track.
        public bool TryGetPendingMetadata(
            string filePath,
            out PendingMetadata metadata)
        {
            metadata = null;

            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string normalizedPath = NormalizePath(filePath);

            lock (_syncRoot)
            {
                return _pendingMetadata.TryGetValue(normalizedPath, out metadata);
            }
        }

        // Check whether a track has unsaved changes.
        public bool HasPendingMetadata(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string normalizedPath = NormalizePath(filePath);

            lock (_syncRoot)
            {
                return _pendingMetadata.ContainsKey(normalizedPath);
            }
        }

        // Get a snapshot of all tracks with pending changes.
        public List<string> GetPendingFilePaths()
        {
            lock (_syncRoot)
            {
                return new List<string>(_pendingMetadata.Keys);
            }
        }

        // Remove pending changes after a successful disk save.
        public void RemovePendingMetadata(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string normalizedPath = NormalizePath(filePath);

            lock (_syncRoot)
            {
                _pendingMetadata.Remove(normalizedPath);
            }
        }

        private string NormalizePath(string filePath)
        {
            return Path.GetFullPath(filePath.Trim());
        }
    }
}

