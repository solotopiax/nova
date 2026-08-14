using System;
using System.Collections.Generic;
using System.Reflection;

using NovaFramework.Runtime;

using NUnit.Framework;

namespace NovaFramework.BestHTTP.Tests
{
    public sealed class BestHttpTelemetryInspectorTests
    {
        [Test]
        public void HttpSettings_BestHttpTelemetry_DefaultsEnabled()
        {
            FieldInfo field = typeof(HttpSettings).GetField("EnableBestHttpTelemetry");

            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetValue(new HttpSettings()), Is.EqualTo(true));
        }

        [Test]
        public void CapabilityDetection_RequiresMatchingEventHandler()
        {
            Type inspectorType = Type.GetType("NovaFramework.Editor.NetworkComponentInspector, NovaFramework.Editor");
            MethodInfo method = inspectorType?.GetMethod(
                "IsBestHttpTelemetryAvailable",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            string requestedTypeName = null;
            Func<string, Type> resolver = typeName =>
            {
                requestedTypeName = typeName;
                return typeof(TelemetryApiStub);
            };

            bool available = (bool)method.Invoke(null, new object[] { resolver });

            Assert.That(available, Is.True);
            Assert.That(
                requestedTypeName,
                Is.EqualTo("Best.HTTP.Telemetry.BestHttpTelemetry, com.Tivadar.Best.HTTP"));
        }

        [Test]
        public void CapabilityDetection_WhenTelemetryApiMissing_ReturnsFalse()
        {
            Type inspectorType = Type.GetType("NovaFramework.Editor.NetworkComponentInspector, NovaFramework.Editor");
            MethodInfo method = inspectorType?.GetMethod(
                "IsBestHttpTelemetryAvailable",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            Func<string, Type> resolver = _ => null;
            bool available = (bool)method.Invoke(null, new object[] { resolver });

            Assert.That(available, Is.False);
        }

        private static class TelemetryApiStub
        {
            public static Action<string, IReadOnlyDictionary<string, object>> EventHandler { get; set; }
        }
    }
}
