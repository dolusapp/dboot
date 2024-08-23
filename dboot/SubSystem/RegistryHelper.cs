using Microsoft.Win32;
using Serilog;

namespace dboot.SubSystem
{

    /// <summary>
    /// Provides a set of static methods for working with the Windows Registry.
    /// Supports common operations such as checking for key existence, getting, setting, and deleting values.
    /// </summary>
    public static class RegistryHelper
    {
        /// <summary>
        /// Checks if a registry key exists at the specified path.
        /// </summary>
        /// <param name="hive">The root hive of the registry (e.g., LocalMachine, CurrentUser).</param>
        /// <param name="subKeyPath">The path to the subkey.</param>
        /// <returns>True if the subkey exists, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="subKeyPath"/> is null or empty.</exception>
        public static bool KeyExists(RegistryHive hive, string subKeyPath)
        {
            if (string.IsNullOrEmpty(subKeyPath))
            {
                Log.Error("SubKeyPath cannot be null or empty.");
                throw new ArgumentException("SubKeyPath cannot be null or empty.", nameof(subKeyPath));
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var subKey = baseKey.OpenSubKey(subKeyPath);
                return subKey != null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check if the registry key exists: {Hive} - {SubKeyPath}", hive, subKeyPath);
                return false;
            }
        }

        /// <summary>
        /// Gets a value from the specified registry key.
        /// Supports string, DWORD (int), and multi-string (string[]) types.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve (string, int, string[]).</typeparam>
        /// <param name="hive">The root hive of the registry (e.g., LocalMachine, CurrentUser).</param>
        /// <param name="subKeyPath">The path to the subkey.</param>
        /// <param name="valueName">The name of the value to retrieve.</param>
        /// <param name="defaultValue">The default value to return if the key or value does not exist.</param>
        /// <returns>The value from the registry, or the default value if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="subKeyPath"/> or <paramref name="valueName"/> is null or empty.</exception>
        public static T? GetValue<T>(RegistryHive hive, string subKeyPath, string valueName, T? defaultValue = default)
        {
            if (string.IsNullOrEmpty(subKeyPath))
            {
                Log.Error("SubKeyPath cannot be null or empty.");
                throw new ArgumentException("SubKeyPath cannot be null or empty.", nameof(subKeyPath));
            }

            if (string.IsNullOrEmpty(valueName))
            {
                Log.Error("ValueName cannot be null or empty.");
                throw new ArgumentException("ValueName cannot be null or empty.", nameof(valueName));
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var subKey = baseKey.OpenSubKey(subKeyPath);
                if (subKey == null)
                {
                    Log.Information("SubKey not found: {Hive} - {SubKeyPath}", hive, subKeyPath);
                    return defaultValue;
                }

                var value = subKey.GetValue(valueName, defaultValue);
                if (value is T result)
                {
                    return result;
                }

                Log.Warning("Value type mismatch or value not found: {Hive} - {SubKeyPath} - {ValueName}", hive, subKeyPath, valueName);
                return defaultValue;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get registry value: {Hive} - {SubKeyPath} - {ValueName}", hive, subKeyPath, valueName);
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a value in the specified registry key.
        /// Supports string, DWORD (int), and multi-string (string[]) types.
        /// </summary>
        /// <typeparam name="T">The type of the value to set (string, int, string[]).</typeparam>
        /// <param name="hive">The root hive of the registry (e.g., LocalMachine, CurrentUser).</param>
        /// <param name="subKeyPath">The path to the subkey.</param>
        /// <param name="valueName">The name of the value to set.</param>
        /// <param name="value">The value to set in the registry.</param>
        /// <returns>True if the operation succeeds, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="subKeyPath"/> or <paramref name="valueName"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when the value type is unsupported.</exception>
        public static bool SetValue<T>(RegistryHive hive, string subKeyPath, string valueName, T value)
        {
            if (string.IsNullOrEmpty(subKeyPath))
            {
                Log.Error("SubKeyPath cannot be null or empty.");
                throw new ArgumentException("SubKeyPath cannot be null or empty.", nameof(subKeyPath));
            }

            if (string.IsNullOrEmpty(valueName))
            {
                Log.Error("ValueName cannot be null or empty.");
                throw new ArgumentException("ValueName cannot be null or empty.", nameof(valueName));
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var subKey = baseKey.CreateSubKey(subKeyPath);
                if (subKey == null)
                {
                    Log.Error("Failed to create or open subkey: {Hive} - {SubKeyPath}", hive, subKeyPath);
                    return false;
                }

                if (value is string stringValue)
                {
                    subKey.SetValue(valueName, stringValue, RegistryValueKind.String);
                }
                else if (value is int dwordValue)
                {
                    subKey.SetValue(valueName, dwordValue, RegistryValueKind.DWord);
                }
                else if (value is string[] multiStringValue)
                {
                    subKey.SetValue(valueName, multiStringValue, RegistryValueKind.MultiString);
                }
                else
                {
                    Log.Error("Unsupported value type: {Type}", typeof(T));
                    throw new ArgumentException("Unsupported value type", nameof(value));
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set registry value: {Hive} - {SubKeyPath} - {ValueName}", hive, subKeyPath, valueName);
                return false;
            }
        }

        /// <summary>
        /// Creates a registry key at the specified path if it doesn't already exist.
        /// </summary>
        /// <param name="hive">The root hive of the registry (e.g., LocalMachine, CurrentUser).</param>
        /// <param name="subKeyPath">The path to the subkey.</param>
        /// <returns>True if the key was created successfully or already exists, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="subKeyPath"/> is null or empty.</exception>
        public static bool CreateKey(RegistryHive hive, string subKeyPath)
        {
            if (string.IsNullOrEmpty(subKeyPath))
            {
                Log.Error("SubKeyPath cannot be null or empty.");
                throw new ArgumentException("SubKeyPath cannot be null or empty.", nameof(subKeyPath));
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var subKey = baseKey.CreateSubKey(subKeyPath);

                // Return true if the subkey was successfully created or already exists.
                return subKey != null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create registry key: {Hive} - {SubKeyPath}", hive, subKeyPath);
                return false;
            }
        }

        /// <summary>
        /// Updates or creates a registry key and performs a series of operations on it.
        /// </summary>
        /// <param name="hive">The registry hive where the key is located.</param>
        /// <param name="subKeyPath">The path to the subkey within the specified hive.</param>
        /// <param name="updateAction">An action delegate that performs operations on the opened registry key.</param>
        /// <returns>True if the key was successfully updated or created, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="subKeyPath"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="updateAction"/> is null.</exception>
        /// <remarks>
        /// This method opens or creates the specified registry key with read/write permissions,
        /// executes the provided action on the key, and then closes the key. It handles any
        /// exceptions that occur during the process and logs them.
        /// </remarks>
        public static bool UpdateKey(RegistryHive hive, string subKeyPath, Action<RegistryKey> updateAction)
        {
            if (string.IsNullOrEmpty(subKeyPath))
            {
                Log.Error("SubKeyPath cannot be null or empty.");
                throw new ArgumentException("SubKeyPath cannot be null or empty.", nameof(subKeyPath));
            }

            if (updateAction == null)
            {
                Log.Error("UpdateAction cannot be null.");
                throw new ArgumentNullException(nameof(updateAction));
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var subKey = baseKey.CreateSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (subKey == null)
                {
                    Log.Error("Failed to create or open subkey: {Hive} - {SubKeyPath}", hive, subKeyPath);
                    return false;
                }

                updateAction(subKey);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update registry key: {Hive} - {SubKeyPath}", hive, subKeyPath);
                return false;
            }
        }

        /// <summary>
        /// Deletes a value from the specified registry key.
        /// </summary>
        /// <param name="hive">The root hive of the registry (e.g., LocalMachine, CurrentUser).</param>
        /// <param name="subKeyPath">The path to the subkey.</param>
        /// <param name="valueName">The name of the value to delete.</param>
        /// <returns>True if the value was successfully deleted, false otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="subKeyPath"/> or <paramref name="valueName"/> is null or empty.</exception>
        public static bool DeleteValue(RegistryHive hive, string subKeyPath, string valueName)
        {
            if (string.IsNullOrEmpty(subKeyPath))
            {
                Log.Error("SubKeyPath cannot be null or empty.");
                throw new ArgumentException("SubKeyPath cannot be null or empty.", nameof(subKeyPath));
            }

            if (string.IsNullOrEmpty(valueName))
            {
                Log.Error("ValueName cannot be null or empty.");
                throw new ArgumentException("ValueName cannot be null or empty.", nameof(valueName));
            }

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var subKey = baseKey.OpenSubKey(subKeyPath, writable: true);
                if (subKey == null)
                {
                    Log.Information("SubKey not found: {Hive} - {SubKeyPath}", hive, subKeyPath);
                    return false;
                }

                subKey.DeleteValue(valueName, throwOnMissingValue: false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete registry value: {Hive} - {SubKeyPath} - {ValueName}", hive, subKeyPath, valueName);
                return false;
            }
        }
    }
}