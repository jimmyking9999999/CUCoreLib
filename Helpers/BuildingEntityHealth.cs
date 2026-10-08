using System;
using System.Linq.Expressions;
using System.Reflection;

namespace CUCoreLib.Helpers
{
    // Fullgame changes the field to a property 
    public static class BuildingEntityHealth
    {
        private static readonly Func<BuildingEntity, float> Getter;
        private static readonly Action<BuildingEntity, float> Setter;

        static BuildingEntityHealth()
        {
            var type = typeof(BuildingEntity);
            var instance = Expression.Parameter(type, "building");
            var value = Expression.Parameter(typeof(float), "value");

            MemberExpression access;
            var property = type.GetProperty("health", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead && property.CanWrite)
            {
                access = Expression.Property(instance, property);
            }
            else
            {
                var field = type.GetField("health", BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    throw new MissingMemberException();
                access = Expression.Field(instance, field);
            }

            Getter = Expression.Lambda<Func<BuildingEntity, float>>(access, instance).Compile();
            Setter = Expression.Lambda<Action<BuildingEntity, float>>(
                Expression.Assign(access, value), instance, value).Compile();
        }

        public static float GetHealth(this BuildingEntity building)
        {
            return building != null ? Getter(building) : 0f;
        }

        public static void SetHealth(this BuildingEntity building, float value)
        {
            if (building != null) Setter(building, value);
        }
    }
}
