using System.Text.Json;
using System.Text.Json.Serialization;

namespace BootstrapperShared;

public class CatalogJsonConverter : JsonConverter<Catalog>
{
    public override Catalog Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        var catalog = new Catalog(new Dictionary<string, BranchInfo>());

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return catalog;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new JsonException("Property name is null or empty");
            }

            if (propertyName != "branches")
            {
                throw new JsonException($"Expected 'branches' property, but found '{propertyName}'");
            }

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of branches object");
            }

            var branches = new Dictionary<string, BranchInfo>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                var branchName = reader.GetString();
                if (string.IsNullOrEmpty(branchName))
                {
                    throw new JsonException("Branch name is null or empty");
                }

                reader.Read();
                var branchInfo = ReadBranchInfo(ref reader, branchName, options);
                branches[branchName] = branchInfo;
            }

            catalog = new Catalog(branches);
        }

        throw new JsonException("Expected end of object");
    }

    private static BranchInfo ReadBranchInfo(ref Utf8JsonReader reader, string branchName, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of branch info object");
        }

        string? currentVersion = null;
        var versions = new Dictionary<string, VersionInfo>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new JsonException("Property name is null or empty");
            }

            reader.Read();

            if (propertyName == "currentVersion")
            {
                currentVersion = reader.GetString();
            }
            else if (propertyName == "versions")
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("Expected start of versions object");
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    var versionKey = reader.GetString();
                    if (string.IsNullOrEmpty(versionKey))
                    {
                        throw new JsonException("Version key is null or empty");
                    }

                    reader.Read();
                    var versionInfo = ReadVersionInfo(ref reader, options);
                    versions[versionKey] = versionInfo;
                }
            }
            else
            {
                throw new JsonException($"Unexpected property '{propertyName}' in branch info object");
            }
        }

        if (string.IsNullOrEmpty(currentVersion))
        {
            throw new JsonException("BranchInfo missing 'currentVersion'");
        }

        return new BranchInfo(branchName, currentVersion, versions);
    }

    private static VersionInfo ReadVersionInfo(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of version info object");
        }

        string? releasePath = null;
        string? releaseHash = null;
        var files = new List<CatalogFile>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new JsonException("Property name is null or empty");
            }

            reader.Read();

            if (propertyName == "releasePath")
            {
                releasePath = reader.GetString();
            }
            else if (propertyName == "releaseHash")
            {
                releaseHash = reader.GetString();
            }
            else if (propertyName == "files")
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new JsonException("Expected start of files array");
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }

                    var file = ReadCatalogFile(ref reader, options);
                    files.Add(file);
                }
            }
            else
            {
                throw new JsonException($"Unexpected property '{propertyName}' in version info object");
            }
        }

        if (string.IsNullOrEmpty(releasePath))
        {
            throw new JsonException("VersionInfo missing 'releasePath'");
        }

        if (string.IsNullOrEmpty(releaseHash))
        {
            throw new JsonException("VersionInfo missing 'releaseHash'");
        }

        return new VersionInfo(releasePath, releaseHash, files);
    }

    private static CatalogFile ReadCatalogFile(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of file object");
        }

        string? path = null;
        string? hash = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new JsonException("Property name is null or empty");
            }

            reader.Read();

            if (propertyName == "path")
            {
                path = reader.GetString();
            }
            else if (propertyName == "hash")
            {
                hash = reader.GetString();
            }
            else
            {
                throw new JsonException($"Unexpected property '{propertyName}' in file object");
            }
        }

        if (string.IsNullOrEmpty(path))
        {
            throw new JsonException("CatalogFile missing 'path'");
        }

        if (string.IsNullOrEmpty(hash))
        {
            throw new JsonException("CatalogFile missing 'hash'");
        }

        return new CatalogFile(path, hash);
    }

    public override void Write(Utf8JsonWriter writer, Catalog value, JsonSerializerOptions options)
    {
        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        writer.WriteStartObject();
        writer.WritePropertyName("branches");
        writer.WriteStartObject();

        foreach (var branch in value.Branches)
        {
            if (string.IsNullOrEmpty(branch.Key))
            {
                throw new JsonException("Branch name is null or empty");
            }
            writer.WritePropertyName(branch.Key);
            writer.WriteStartObject();

            if (string.IsNullOrEmpty(branch.Value.CurrentVersion))
            {
                throw new JsonException($"Current version for branch '{branch.Key}' is null or empty");
            }
            writer.WriteString("currentVersion", branch.Value.CurrentVersion);

            writer.WritePropertyName("versions");
            writer.WriteStartObject();

            foreach (var version in branch.Value.Versions)
            {
                if (string.IsNullOrEmpty(version.Key))
                {
                    throw new JsonException($"Version key in branch '{branch.Key}' is null or empty");
                }
                writer.WritePropertyName(version.Key);
                writer.WriteStartObject();

                if (string.IsNullOrEmpty(version.Value.ReleasePath))
                {
                    throw new JsonException($"ReleasePath for version '{version.Key}' in branch '{branch.Key}' is null or empty");
                }
                writer.WriteString("releasePath", version.Value.ReleasePath);

                if (string.IsNullOrEmpty(version.Value.ReleaseHash))
                {
                    throw new JsonException($"ReleaseHash for version '{version.Key}' in branch '{branch.Key}' is null or empty");
                }
                writer.WriteString("releaseHash", version.Value.ReleaseHash);

                writer.WritePropertyName("files");
                writer.WriteStartArray();

                foreach (var file in version.Value.Files)
                {
                    writer.WriteStartObject();
                    if (string.IsNullOrEmpty(file.Path))
                    {
                        throw new JsonException($"File path in version '{version.Key}' of branch '{branch.Key}' is null or empty");
                    }
                    writer.WriteString("path", file.Path);
                    if (string.IsNullOrEmpty(file.Hash))
                    {
                        throw new JsonException($"File hash for '{file.Path}' in version '{version.Key}' of branch '{branch.Key}' is null or empty");
                    }
                    writer.WriteString("hash", file.Hash);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject(); // end versions
            writer.WriteEndObject(); // end branch
        }

        writer.WriteEndObject(); // end branches
        writer.WriteEndObject(); // end catalog
    }
}
