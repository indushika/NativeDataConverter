using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static UnityEditor.Rendering.FilterWindow;

public static class GenericNativeDataGenerator 
{
    private const string generatedPath = "Assets/Scripts/GenerateNativeData/";
    private static Dictionary<string, DateTime> filesByLastModifiedTime = default;

    private static string structName;
    private static string structFileName;
    private static Type classType;

    [MenuItem("Tools/Generate Native Data")]
    public static void GenerateNativeDataScripts()
    {
        Debug.Log("GenericNativeDataGenerator: GenerateNativeDataScripts");

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            //Debug.Log("GenericNativeDataGenerator: GenerateNativeDataScripts: Type: " + type.Name);

            if ((type.IsClass || type.IsStruct()) && type.GetCustomAttribute<GenerateNativeDataAttribute>() != null)
            {
                Debug.Log("GenericNativeDataGenerator: GenerateNativeDataScripts: Type: " + type.Name );
                GenerateNativeDataForType(type);
            }
        }
    }

    public static void GenerateNativeDataForType(Type type) 
    {
        classType = type;
        structName = "Native" + type.Name;
        structFileName = structName + ".cs";

        GenerateStruct(type, structName, structFileName);
    }

    private static void GenerateStruct(Type type, string structName, string structFileName)
    {
        var listFields = new Dictionary<string, string>();
        var dictionaryFields = new Dictionary<string, (Type, string)>();
        var vector2Fields = new List<string>();
        var vector3Fields = new List<string>();
        var valueTypeFields = new Dictionary<string, string>();

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var fieldType = field.FieldType;
            var isGenericType = fieldType.IsGenericType;

            if (isGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = fieldType.GetGenericArguments()[0];

                if (elementType.IsValueType)
                {
                    listFields.Add(field.Name, elementType.Name);
                }

            }
            else if (isGenericType && fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var elementOneType = fieldType.GetGenericArguments()[0];
                var elementTwoType = fieldType.GetGenericArguments()[1];

                if (elementOneType.IsValueType && elementTwoType.IsValueType)
                {
                    dictionaryFields.Add(field.Name, (elementOneType, elementTwoType.Name));
                }

            }
            else if (fieldType == typeof(Vector3))
            {
                vector3Fields.Add(field.Name);

            }
            else if (fieldType == typeof(Vector2))
            {
                vector2Fields.Add(field.Name);
            }
            else if (fieldType.IsPrimitive || fieldType.IsEnum)
            {
                valueTypeFields.Add(field.Name, fieldType.Name);
            }
        }

        ScriptBuilder(listFields, dictionaryFields, vector3Fields, vector2Fields, valueTypeFields);
    }

    private static void ScriptBuilder(Dictionary<string, string> lists = null,
        Dictionary<string, (Type, string)> dictionaries = null,
        List<string> vector3 = null,
        List<string> vector2 = null,
        Dictionary<string, string> valueTypes = null)
    {
        if (lists == null && dictionaries == null && vector2 == null && vector3 == null && valueTypes == null)
        {
            return;
        }

        Debug.Log("GenericNativeDataGenerator: ScriptBuilder");

        var sb = new StringBuilder();
        sb.AppendLine("using Unity.Collections;");
        sb.AppendLine("using Unity.Mathematics;");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"public struct {structName}");
        sb.AppendLine("{");

        foreach (var field in lists)
        {
            sb.AppendLine($"    public NativeArray<{field.Value}> {field.Key};");
        }

        foreach (var field in dictionaries)
        {
            var keyElement = field.Value.Item1.IsEnum ? "int" : field.Value.Item1.Name;
            sb.AppendLine($"    public NativeHashMap<{keyElement},{field.Value.Item2}> {field.Key};");
        }

        foreach (var fieldName in vector3)
        {
            sb.AppendLine($"    public float3 {fieldName};");
        }

        foreach (var fieldName in vector2)
        {
            sb.AppendLine($"    public float2 {fieldName};");
        }

        foreach (var field in valueTypes)
        {
            sb.AppendLine($"    public {field.Value} {field.Key};");
        }


        sb.AppendLine();
        sb.AppendLine($"    public {structName}({classType.Name} instance)");
        sb.AppendLine("    {");

        foreach (var field in lists)
        {
            sb.AppendLine($"        {field.Key} = new NativeArray<{field.Value}>(instance.{field.Key}.Count, Allocator.Persistent);");
            sb.AppendLine();
            sb.AppendLine($"        for (int i = 0; i < instance.{field.Key}.Count; i++)");
            sb.AppendLine($"            {field.Key}[i] = instance.{field.Key}[i];");
            sb.AppendLine();
        }

        foreach (var field in dictionaries)
        {
            var keyElement = field.Value.Item1.IsEnum ? "int" : field.Value.Item1.Name;
            sb.AppendLine($"        {field.Key} = new NativeHashMap<{keyElement},{field.Value.Item2}>" +
                $"(instance.{field.Key}.Count, Allocator.Persistent);");
            sb.AppendLine();

            var elementKey = field.Value.Item1.IsEnum ? "(int)element.Key" : "element.Key";
            
            sb.AppendLine($"        foreach (var element in instance.{field.Key})");
            sb.AppendLine($"            {field.Key}.Add({elementKey}, element.Value);");
            sb.AppendLine();
        }

        foreach (var fieldName in vector2)
        {
            sb.AppendLine($"        {fieldName} = new float2(instance.{fieldName}.x, instance.{fieldName}.y);");
            sb.AppendLine();
        }

        foreach (var fieldName in vector3)
        {
            sb.AppendLine($"        {fieldName} = new float3(instance.{fieldName}.x, instance.{fieldName}.y, instance.{fieldName}.z);");
            sb.AppendLine();
        }
        
        foreach (var field in valueTypes)
        {
            sb.AppendLine($"        {field.Key} = instance.{field.Key};");
            sb.AppendLine();
        }

        sb.AppendLine("    }");


        sb.AppendLine();

        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");

        foreach (var field in lists)
        {
            sb.AppendLine($"        if ({field.Key}.IsCreated)");
            sb.AppendLine($"            {field.Key}.Dispose();");
        }

        foreach (var field in dictionaries)
        {
            sb.AppendLine($"        if ({field.Key}.IsCreated)");
            sb.AppendLine($"            {field.Key}.Dispose();");
        }

        sb.AppendLine("    }");

        sb.AppendLine("}");


        Directory.CreateDirectory(generatedPath);
        File.WriteAllText(generatedPath + structFileName, sb.ToString());

        Debug.Log($"Struct {structName} generated and saved to {generatedPath}{structFileName}");

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif

    }
}
