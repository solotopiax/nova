/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtectedIntegerJsonConverter.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   受保护整数只持久化普通数值，不保存内存密钥
 ***************************************************************/

using System;
using System.Globalization;
using Newtonsoft.Json;

namespace NovaFramework.Runtime
{
    /// <summary>保留普通数字 JSON 契约，插件安装和卸载不会改变存档字段格式。</summary>
    public sealed class ProtectedIntegerJsonConverter : JsonConverter
    {
        /// <summary>只处理 Nova 的两个受保护整数类型。</summary>
        public override bool CanConvert(Type objectType) => objectType == typeof(ProtectedInt) || objectType == typeof(ProtectedLong);

        /// <summary>从整数令牌建立运行时状态；禁止把可能含玩家标识的 JSON 路径当作埋点字段名。</summary>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.Integer) throw new JsonSerializationException("受保护整数存档必须是整数。");
            long value;
            try { value = Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture); }
            catch (Exception) { throw new JsonSerializationException("受保护整数存档超出取值范围。"); }
            string fieldId = existingValue is ProtectedInt oldInt ? oldInt.FieldId :
                existingValue is ProtectedLong oldLong ? oldLong.FieldId :
                objectType == typeof(ProtectedInt) ? "json.protected_int32" : "json.protected_int64";
            if (objectType == typeof(ProtectedInt))
            {
                if (value < int.MinValue || value > int.MaxValue) throw new JsonSerializationException("受保护 int 存档超出取值范围。");
                return new ProtectedInt(fieldId, (int)value);
            }
            return new ProtectedLong(fieldId, value);
        }

        /// <summary>验证后仅写普通数值；隔离字段拒绝序列化，不生成可疑存档。</summary>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null) writer.WriteNull();
            else if (value is ProtectedInt integer && integer.TryRead(out int intValue)) writer.WriteValue(intValue);
            else if (value is ProtectedLong longInteger && longInteger.TryRead(out long longValue)) writer.WriteValue(longValue);
            else throw new JsonSerializationException("受保护整数不可用，已拒绝保存。");
        }
    }
}
