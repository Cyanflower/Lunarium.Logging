// Copyright 2026 Cyanflower
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;

class Program
{
    static void Main()
    {
        Assembly asm = Assembly.LoadFrom("../../src/Lunarium.Logging/bin/Debug/net10.0/Lunarium.Logging.dll");
        var types = asm.GetTypes().Where(t => !t.Name.StartsWith("<")).OrderBy(t => t.Namespace).ThenBy(t => t.Name);
        
        foreach (var type in types)
        {
            if (Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute))) continue;
            
            string visibility = type.IsPublic || type.IsNestedPublic ? "public" : 
                                type.IsNestedAssembly ? "internal" :
                                type.IsNotPublic ? "internal" : 
                                type.IsNestedFamily ? "protected" :
                                type.IsNestedFamORAssem ? "protected internal" :
                                type.IsNestedFamANDAssem ? "private protected" :
                                type.IsNestedPrivate ? "private" : "internal";
            
            string typeKind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : "class";
            if (type.IsAbstract && type.IsSealed) typeKind = "static class";
            else if (type.IsAbstract && !type.IsInterface) typeKind = "abstract class";
            else if (type.IsSealed && type.IsClass) typeKind = "sealed class";
            
            bool isRecord = type.GetMethods().Any(m => m.Name == "<Clone>$");
            if (isRecord) typeKind = type.IsValueType ? "record struct" : "record";

            Console.WriteLine($"\n### `{visibility} {typeKind} {type.FullName}`");
            
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                              .OrderBy(m => m.MemberType).ThenBy(m => m.Name);
            
            bool hasMembers = false;
            
            foreach (var member in members)
            {
                if (member.Name.StartsWith("<") || member.Name.StartsWith("get_") || member.Name.StartsWith("set_") || member.Name.StartsWith("add_") || member.Name.StartsWith("remove_"))
                    continue; 
                if (member is MethodInfo method && method.IsSpecialName)
                    continue; 
                if (Attribute.IsDefined(member, typeof(CompilerGeneratedAttribute))) continue;
                
                string memVis = "private";
                if (member is MethodInfo mi) {
                    if (mi.IsPublic) memVis = "public";
                    else if (mi.IsAssembly) memVis = "internal";
                    else if (mi.IsFamily) memVis = "protected";
                    else if (mi.IsFamilyOrAssembly) memVis = "protected internal";
                    else if (mi.IsFamilyAndAssembly) memVis = "private protected";
                    else memVis = "private";
                }
                else if (member is ConstructorInfo ci) {
                    if (ci.IsPublic) memVis = "public";
                    else if (ci.IsAssembly) memVis = "internal";
                    else if (ci.IsFamily) memVis = "protected";
                    else if (ci.IsFamilyOrAssembly) memVis = "protected internal";
                    else if (ci.IsFamilyAndAssembly) memVis = "private protected";
                    else memVis = "private";
                }
                else if (member is FieldInfo fi) {
                    if (fi.IsPublic) memVis = "public";
                    else if (fi.IsAssembly) memVis = "internal";
                    else if (fi.IsFamily) memVis = "protected";
                    else if (fi.IsFamilyOrAssembly) memVis = "protected internal";
                    else if (fi.IsFamilyAndAssembly) memVis = "private protected";
                    else memVis = "private";
                }
                else if (member is PropertyInfo pi) {
                    var getMethod = pi.GetMethod;
                    var setMethod = pi.SetMethod;
                    bool isPub = (getMethod?.IsPublic == true) || (setMethod?.IsPublic == true);
                    bool isInt = (getMethod?.IsAssembly == true) || (setMethod?.IsAssembly == true);
                    bool isProt = (getMethod?.IsFamily == true) || (setMethod?.IsFamily == true);
                    bool isProtInt = (getMethod?.IsFamilyOrAssembly == true) || (setMethod?.IsFamilyOrAssembly == true);
                    if (isPub) memVis = "public";
                    else if (isProtInt) memVis = "protected internal";
                    else if (isProt) memVis = "protected";
                    else if (isInt) memVis = "internal";
                }
                
                string staticMarker = "";
                if (member is MethodInfo smi && smi.IsStatic && typeKind != "static class") staticMarker = " static";
                if (member is PropertyInfo spi && (spi.GetMethod?.IsStatic == true || spi.SetMethod?.IsStatic == true)) staticMarker = " static";
                if (member is FieldInfo sfi && sfi.IsStatic) staticMarker = " static";
                string typeStr = member.MemberType.ToString().ToLower();
                if (member is ConstructorInfo) typeStr = "constructor";
                Console.WriteLine($"- `{memVis}{staticMarker} {typeStr} {member.Name}`");
                hasMembers = true;
            }
            
            if (!hasMembers) {
                Console.WriteLine("- *(No explicit members)*");
            }
        }
    }
}
