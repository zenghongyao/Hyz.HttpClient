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
                // 处理各种数字类型
                if (reader.TryGetInt32(out int intValue))
                    return intValue.ToString();
                if (reader.TryGetInt64(out long longValue))
                    return longValue.ToString();
                if (reader.TryGetDouble(out double doubleValue))
                    return doubleValue.ToString();
                if (reader.TryGetDecimal(out decimal decimalValue))
                    return decimalValue.ToString();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }

            // 如果是其他类型，转换为字符串
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
                    // 返回默认时间（如 DateTime.MinValue）或根据需求抛出明确异常
                    return default;
                    // 或者抛出异常：throw new JsonException("无法将空字符串转换为 DateTime。");
                }
                if (DateTime.TryParse(stringValue, out var dateTime))
                {
                    return dateTime;
                }
            }

            // 处理其他非字符串类型（如数字）
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
            writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFK"));
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

                // 处理空字符串或 null 字符串
                if (string.IsNullOrEmpty(stringValue))
                {
                    return null;
                }

                // 尝试解析日期时间
                if (DateTime.TryParse(stringValue, out var dateTime))
                {
                    return dateTime;
                }

                // 如果无法解析，抛出明确的异常
                throw new JsonException($"无法将值 '{stringValue}' 转换为 DateTime。");
            }

            // 处理其他类型（如数字时间戳）
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
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFK"));
            }
        }
    }



    /// <summary>
    /// 弹性枚举转换器，支持字符串和数字两种方式
    /// </summary>
    public class FlexibleEnumConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            return (JsonConverter)Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(type));
        }

        private class EnumConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            // 获取枚举的基础类型
            private readonly Type _underlyingType = Enum.GetUnderlyingType(typeof(T));

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                JsonTokenType tokenType = reader.TokenType;

                // 处理字符串类型的值
                if (tokenType == JsonTokenType.String)
                {
                    string? enumString = reader.GetString();
                    if (Enum.TryParse(enumString, true, out T value))
                        return value;
                }
                // 处理数字类型的值
                else if (tokenType == JsonTokenType.Number)
                {
                    try
                    {
                        // 根据基础类型动态解析数字值
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
                        // 数字转换失败时尝试字符串回退
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
                // 直接写入数字值（根据实际基础类型）
                switch (Convert.GetTypeCode(Enum.ToObject(typeof(T), value)))
                {
                    case TypeCode.Byte:
                        writer.WriteNumberValue(Convert.ToByte(value));
                        break;
                    case TypeCode.SByte:
                        writer.WriteNumberValue(Convert.ToSByte(value));
                        break;
                    case TypeCode.Int16:
                        writer.WriteNumberValue(Convert.ToInt16(value));
                        break;
                    case TypeCode.UInt16:
                        writer.WriteNumberValue(Convert.ToUInt16(value));
                        break;
                    case TypeCode.Int32:
                        writer.WriteNumberValue(Convert.ToInt32(value));
                        break;
                    case TypeCode.UInt32:
                        writer.WriteNumberValue(Convert.ToUInt32(value));
                        break;
                    case TypeCode.Int64:
                        writer.WriteNumberValue(Convert.ToInt64(value));
                        break;
                    case TypeCode.UInt64:
                        writer.WriteNumberValue(Convert.ToUInt64(value));
                        break;
                    default:
                        writer.WriteStringValue(value.ToString());
                        break;
                }
            }
        }
    }

}
