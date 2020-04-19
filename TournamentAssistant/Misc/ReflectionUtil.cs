using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/**
 * Modified by Moon on 8/20/2018
 * Code originally pulled from xyonico's repo, modified to suit my needs
 * (https://github.com/xyonico/BeatSaberSongLoader/blob/master/SongLoaderPlugin/ReflectionUtil.cs)
 */

namespace TournamentAssistant.Misc
{
    public static class ReflectionUtil
    {
        private const BindingFlags _allBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        //Sets the value of a (static?) field in object "obj" with name "fieldName"
        public static void SetField(this object obj, string fieldName, object value, Type overrideType = null)
        {
            (obj is Type ? (Type)obj : (overrideType == null ? obj.GetType() : overrideType))
                .GetField(fieldName, _allBindingFlags)
                .SetValue(obj, value);
        }

        //Gets the value of a (static?) field in object "obj" with name "fieldName"
        public static object GetField(this object obj, string fieldName, Type overrideType = null)
        {
            return (obj is Type ? (Type)obj : (overrideType == null ? obj.GetType() : overrideType))
                .GetField(fieldName, _allBindingFlags)
                .GetValue(obj);
        }

        //Gets the value of a (static?) field in object "obj" with name "fieldName" (TYPED)
        public static T GetField<T>(this object obj, string fieldName, Type overrideType = null) => (T)GetField(obj, fieldName, overrideType);

        //Sets the value of a (static?) Property specified by the object "obj" and the name "propertyName"
        public static void SetProperty(this object obj, string propertyName, object value, Type overrideType = null)
        {
            (obj is Type ? (Type)obj : (overrideType == null ? obj.GetType() : overrideType))
                .GetProperty(propertyName, _allBindingFlags)
                .SetValue(obj, value, null);
        }

        //Gets the value of a (static?) Property specified by the object "obj" and the name "propertyName"
        public static object GetProperty(this object obj, string propertyName, Type overrideType = null)
        {
            return (obj is Type ? (Type)obj : (overrideType == null ? obj.GetType() : overrideType))
                .GetProperty(propertyName, _allBindingFlags)
                .GetValue(obj);
        }

        //Gets the value of a (static?) Property specified by the object "obj" and the name "propertyName" (TYPED)
        public static T GetProperty<T>(this object obj, string propertyName) => (T)GetProperty(obj, propertyName);

        //Invokes a (static?) private method with name "methodName" and params "methodParams", returns an object of the specified type
        public static T InvokeMethod<T>(this object obj, string methodName, params object[] methodParams) => (T)InvokeMethod(obj, methodName, null, methodParams);
        public static T InvokeMethod<T>(this object obj, string methodName, Type overrideType, params object[] methodParams) => (T)InvokeMethod(obj, methodName, overrideType, methodParams);

        //Invokes a (static?) private method with name "methodName" and params "methodParams"
        public static object InvokeMethod(this object obj, string methodName, params object[] methodParams) => InvokeMethod(obj, methodName, null, methodParams);
        public static object InvokeMethod(this object obj, string methodName, Type overrideType, params object[] methodParams)
        {
            return (obj is Type ? (Type)obj : (overrideType == null ? obj.GetType() : overrideType))
                .GetMethod(methodName, _allBindingFlags)
                .Invoke(obj, methodParams);
        }

        //Returns a constructor with the specified parameters to the specified type or object
        public static object InvokeConstructor(this object obj, params object[] constructorParams)
        {
            Type[] types = new Type[constructorParams.Length];
            for (int i = 0; i < constructorParams.Length; i++) types[i] = constructorParams[i].GetType();
            return (obj is Type ? (Type)obj : obj.GetType())
                .GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null)
                .Invoke(constructorParams);
        }

        //Returns a Type object which can be used to invoke static methods with the above helpers
        public static Type GetStaticType(string clazz)
        {
            return Type.GetType(clazz);
        }

        //Returns a list (of strings) of the names of all loaded assemblies
        public static IEnumerable<Assembly> ListLoadedAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }

        //Returns a list of all loaded namespaces
        //TODO: Check up on time complexity here, could potentially be parallelized
        public static IEnumerable<string> ListNamespacesInAssembly(Assembly assembly)
        {
            IEnumerable<string> ret = Enumerable.Empty<string>();
            ret = ret.Concat(assembly.GetTypes()
                    .Select(t => t.Namespace)
                    .Distinct()
                    .Where(n => n != null));
            return ret.Distinct();
        }

        //Returns a list of classes in a namespace
        //TODO: Check up on time complexity here, could potentially be parallelized
        public static IEnumerable<string> ListClassesInNamespace(string ns)
        {
            //For each loaded assembly
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                //If the assembly contains the desired namespace
                if (assembly.GetTypes().Where(t => t.Namespace == ns).Any())
                {
                    //Select the types we want from the namespace and return them
                    return assembly.GetTypes()
                        .Where(t => t.IsClass)
                        .Select(t => t.Name);
                }
            }
            return null;

            //Code to list reflectable classes
            /*
            ReflectionUtil.ListLoadedAssemblies().ToList().ForEach(x => {
                if (x.GetName().Name == "BeatSaberMultiplayer")
                {
                    Logger.Success($"ASSEMBLY: {x.GetName().Name}");
                    ReflectionUtil.ListNamespacesInAssembly(x).ToList().ForEach(y =>
                    {
                        Logger.Warning($"NAMESPACE: {y}");
                        ReflectionUtil.ListClassesInNamespace(y).ToList().ForEach(z =>
                        {
                            Logger.Warning($"CLASS: {z} : {((ReflectionUtil.GetStaticType(y + "." + z + "," + x) != null) ? "REFLECTABLE" : "NOT")}");
                        });
                    });
                }
            });
            */
        }
    }
}
