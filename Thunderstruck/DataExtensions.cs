﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Thunderstruck
{
    public static class DataExtensions
    {
        #region Utils

        public static PropertyInfo[] GetValidPropertiesOf(Type type)
        {
            var ignore = typeof(DataIgnoreAttribute);

            return type
                .GetProperties()
                .Where(p =>
                    !p.PropertyType.IsInterface &&
                    p.PropertyType.Name != "DataQueryObject`1" &&
                    p.PropertyType.Name != "DataCommands`1" &&
                    p.GetCustomAttributes(ignore, false).Length == 0)
                .ToArray();
        }

        public static PropertyInfo GetPrimaryKey(Type type)
        {
            return GetValidPropertiesOf(type).First();
        }

        #endregion

        #region SqlDataReader

        public static T[] ToArray<T>(this IDataReader reader) where T : new()
        {
            var list = new List<T>();
            var properties = typeof(T).GetProperties();
            var readerFields = GetReaderFields(reader);

            while (reader.Read())
            {
                var item = new T();

                foreach (var field in readerFields)
                {
                    var property = properties.FirstOrDefault(p => p.Name.ToUpper() == field.ToUpper());
                    if (property == null) continue;

                    try
                    {
                        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        object safeValue = (reader[field] == null) ? null : Convert.ChangeType(reader[field], propertyType);
                        property.SetValue(item, safeValue, null);
                    }
                    catch (FormatException err)
                    {
                        var message = String.Format("Erro to convert column {0} to property {1} {2}.{3}",
                            property.Name, property.PropertyType.Name, typeof(T).Name, property.Name);

                        throw new FormatException(message, err);
                    }
                }

                list.Add(item);
            }

            reader.Close();

            return list.ToArray();
        }

        private static string[] GetReaderFields(IDataReader reader)
        {
            var fields = new String[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++) fields[i] = reader.GetName(i);

            return fields;
        }

        #endregion

        #region SqlCommand

        public static void AddParameters(this IDbCommand command, object objectParameters)
        {
            var dictionary = objectParameters as Dictionary<string, object> ?? CreateDictionary(objectParameters);

            foreach (var item in dictionary)
            {
                var parameterName = String.Concat("@", item.Key);

                if (command.CommandText.Contains(parameterName))
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = parameterName;
                    parameter.Value = item.Value ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }
            }
        }

        private static Dictionary<string, object> CreateDictionary(object objectParameters)
        {
            var dictionary = new Dictionary<string, object>();

            foreach (var p in GetValidPropertiesOf(objectParameters.GetType()))
            {
                dictionary.Add(p.Name, p.GetValue(objectParameters, null));
            }

            return dictionary;
        }

        #endregion

        #region DataContext

        /// <summary>
        /// Executes a sql query. Avoid.
        /// </summary>
        /// <param name="query">Query sql to execute on database.</param>
        /// <param name="queryParams">Object that contains parameters to bind in query.</param>
        /// <returns>An open data reader.</returns>

        /// <summary>
        /// Executes a sql query and return all results in array.
        /// </summary>
        /// <typeparam name="T">Type of object to bind each row of the result.</typeparam>
        /// <param name="data">Data context to run query.</param>
        /// <param name="query">Query sql to execute on database.</param>
        /// <param name="queryParams">Object that contains parameters to bind in query.</param>
        /// <returns>All row of query result in array of specified type.</returns>
        public static T[] All<T>(this DataContext data, string query, object queryParams = null) where T : new()
        {
            return data.Query(query, queryParams).ToArray<T>();
        }

        /// <summary>
        /// Executes a sql query and return first results.
        /// </summary>
        /// <typeparam name="T">Type of object to bind first row of the result.</typeparam>
        /// <param name="data">Data context to run query.</param>
        /// <param name="query">Query sql to execute on database.</param>
        /// <param name="queryParams">Object that contains parameters to bind in query.</param>
        /// <returns>First row of query result in specified type.</returns>
        public static T First<T>(this DataContext data, string query, object queryParams = null) where T : new()
        {
            return data.All<T>(query, queryParams).FirstOrDefault();
        }

        #endregion
    }
}