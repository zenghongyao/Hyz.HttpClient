using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hyz.HttpClient
{
    /// <summary>
    /// 字符串数字转换器
    /// </summary>
    public class StringNumberConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                // 先尝试整数类型以保持精度（double 可能丢失大整数精度）
                if (reader.TryGetInt64(out long longValue))
                    return longValue.ToString();
                // 对于极大数或小数，尝试 decimal 精度
                if (reader.TryGetDecimal(out decimal decimalValue))
                    return decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                // 最后尝试 double
                if (reader.TryGetDouble(out double doubleValue))
                    return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }

            return reader.GetString() ?? string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// 日期时间转换器
    /// </summary>
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue))
                {
                    return default;
                }
                if (DateTime.TryParse(stringValue, out var dateTime))
                {
                    return dateTime;
                }
            }

            try
            {
                return reader.GetDateTime();
            }
            catch (FormatException ex)
            {
                throw new JsonException("无效的 DateTime 格式。", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(HttpClientPolicy.DateTimeFormat));
        }
    }

    /// <summary>
    /// 可空日期时间转换器
    /// </summary>
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? stringValue = reader.GetString();

                if (string.IsNullOrEmpty(stringValue))
                {
                    return null;
                }

                if (DateTime.TryParse(stringValue, out var dateTime))
                {
                    return dateTime;
                }

                throw new JsonException($"无法将值 '{stringValue}' 转换为 DateTime。");
            }

            try
            {
                return reader.GetDateTime();
            }
            catch (Exception ex)
            {
                throw new JsonException("无效的 DateTime 格式。", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.Value.ToString(HttpClientPolicy.DateTimeFormat));
            }
        }
    }



    /// <summary>
    /// 弹性枚举转换器，支持字符串和数字两种方式
    /// </summary>
    public class FlexibleEnumConverter : JsonConverterFactory
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, JsonConverter> _converterCache = new System.Collections.Concurrent.ConcurrentDictionary<Type, JsonConverter>();

        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            return _converterCache.GetOrAdd(type, t =>
                (JsonConverter)Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(t))!);
        }

        private class EnumConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            private readonly Type _underlyingType = Enum.GetUnderlyingType(typeof(T));
            private readonly Action<Utf8JsonWriter, T> _writeAction;

            public EnumConverter()
            {
                // 编译期绑定写入方法，避免运行时反射
                _writeAction = _underlyingType.Name switch
                {
                    nameof(Int32) => (w, v) => w.WriteNumberValue(Convert.ToInt32(v)),
                    nameof(UInt32) => (w, v) => w.WriteNumberValue(Convert.ToUInt32(v)),
                    nameof(Int64) => (w, v) => w.WriteNumberValue(Convert.ToInt64(v)),
                    nameof(UInt64) => (w, v) => w.WriteNumberValue(Convert.ToUInt64(v)),
                    nameof(Int16) => (w, v) => w.WriteNumberValue(Convert.ToInt16(v)),
                    nameof(UInt16) => (w, v) => w.WriteNumberValue(Convert.ToUInt16(v)),
                    nameof(Byte) => (w, v) => w.WriteNumberValue(Convert.ToByte(v)),
                    nameof(SByte) => (w, v) => w.WriteNumberValue(Convert.ToSByte(v)),
                    _ => (w, v) => w.WriteStringValue(v.ToString())
                };
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                JsonTokenType tokenType = reader.TokenType;

                if (tokenType == JsonTokenType.String)
                {
                    string? enumString = reader.GetString();
                    if (Enum.TryParse(enumString, true, out T value))
                        return value;
                }
                else if (tokenType == JsonTokenType.Number)
                {
                    try
                    {
                        object numericValue = _underlyingType.Name switch
                        {
                            nameof(Int32) => reader.GetInt32(),
                            nameof(UInt32) => reader.GetUInt32(),
                            nameof(Int64) => reader.GetInt64(),
                            nameof(UInt64) => reader.GetUInt64(),
                            nameof(Int16) => (short)reader.GetInt32(),
                            nameof(UInt16) => (ushort)reader.GetUInt32(),
                            nameof(Byte) => (byte)reader.GetUInt32(),
                            nameof(SByte) => (sbyte)reader.GetInt32(),
                            _ => throw new JsonException($"Unsupported underlying type: {_underlyingType}")
                        };

                        return (T)Enum.ToObject(typeof(T), numericValue);
                    }
                    catch
                    {
                        string stringValue = reader.TryGetInt64(out long longVal)
                            ? longVal.ToString()
                            : reader.GetDouble().ToString();

                        if (Enum.TryParse(stringValue, true, out T value))
                            return value;
                    }
                }

                throw new JsonException($"Unable to convert value to {typeof(T).Name}");
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                _writeAction(writer, value);
            }
        }
    }

}
