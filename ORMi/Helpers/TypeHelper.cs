﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ORMi.Helpers
{
    public static class TypeHelper
    {
        public static object LoadObject(ManagementObject mo, Type t)
        {
            var o = Activator.CreateInstance(t);

            foreach (PropertyInfo p in o.GetType().GetProperties())
            {
                _SetPropertyValue(mo, p, o);
            }

            return o;
        }

        public static dynamic LoadDynamicObject(ManagementBaseObject mo)
        {
            dynamic res = new ExpandoObject();

            var properties = (IDictionary<string, object>)res;

            foreach (PropertyData p in mo.Properties)
            {
                properties.Add(p.Name, p.Value);
            }

            return res;
        }

        public static object LoadObject(ManagementBaseObject mbo, Type t, ManagementScope fallbackScope = null)
        {
            if (mbo is ManagementObject mo)
                return LoadObject(mo, t);

            var o = Activator.CreateInstance(t);

            foreach (PropertyInfo p in o.GetType().GetProperties())
            {
                _SetPropertyValue(mbo, p, o, fallbackScope);
            }

            return o;
        }

        private static void _SetPropertyValue(ManagementBaseObject mo, PropertyInfo p, object o, ManagementScope fallbackScope = null)
        {
            WMIIgnore ignoreProp = p.GetCustomAttribute<WMIIgnore>();

            if (ignoreProp == null)
            {
                WMIProperty propAtt = p.GetCustomAttribute<WMIProperty>();

                string propertyName = String.Empty;

                if (propAtt != null)
                {
                    propertyName = propAtt.Name;
                }
                else
                {
                    propertyName = p.Name;
                }

                var a = mo.Properties[propertyName].Value;

                if (a == null)
                {
                    p.SetValue(o, null);
                }
                else if (p.PropertyType == typeof(DateTime) && a is string s)
                {
                    p.SetValue(o, ManagementDateTimeConverter.ToDateTime((string)a), null);
                }
                else if (p.PropertyType.IsEnum && Enum.IsDefined(p.PropertyType, a))
                {
                    p.SetValue(o, Enum.ToObject(p.PropertyType, a));
                }
                else if (typeof(WMIInstance).IsAssignableFrom(p.PropertyType) && a is ManagementBaseObject nestedMo)
                {
                    // If the property is another WMI instance type, load it
                    p.SetValue(o, LoadObject(nestedMo, p.PropertyType, fallbackScope));
                }
                else
                {
                    var propertyType = p.PropertyType;
                    if (propertyType.IsGenericType &&
                        propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        propertyType = propertyType.GetGenericArguments()[0];
                    }
                    p.SetValue(o, Convert.ChangeType(a, propertyType), null);
                }
            }
            else
            {
                if (o.GetType().BaseType == typeof(WMIInstance) && p.Name == nameof(WMIInstance.Scope))
                {
                    // If we have a ManagementObject with a scope, use that
                    if (mo is ManagementObject smo)
                    {
                        p.SetValue(o, smo.Scope);
                    }
                    // Otherwise, use a fallback scope if provided
                    else if(fallbackScope != null)
                    {
                        p.SetValue(o, fallbackScope);
                    }
                }
            }
        }

        public static string GetClassName(object p)
        {
            var dnAttribute = p.GetType().GetCustomAttribute<WMIClass>(true);

            if (dnAttribute != null)
            {
                return dnAttribute.Name;
            }
            else
            {
                return p.GetType().Name;
            }
        }

        public static string GetClassName(Type t)
        {
            var dnAttribute = t.GetCustomAttribute<WMIClass>(true);

            if (dnAttribute != null)
            {
                return dnAttribute.Name;
            }
            else
            {
                return t.Name;
            }
        }

        public static string GetNamespace(object o)
        {
            if (o.GetType().IsSubclassOf(typeof(WMIInstance)))
            {
                return ((WMIInstance)(o)).Scope.Path.NamespacePath;
            }
            else
            {
                var dnAttribute = o.GetType().GetCustomAttribute<WMIClass>(true);

                if (dnAttribute != null)
                {
                    return dnAttribute.Namespace;
                }
            }

            return null;
        }

        public static string GetNamespace(Type t)
        {
            var dnAttribute = t.GetCustomAttribute<WMIClass>(true);

            if (dnAttribute != null)
            {
                return dnAttribute.Namespace;
            }

            return null;
        }

        public static ManagementObject GetManagementObject(ManagementClass sourceClass, object obj)
        {
            ManagementObject genericInstance;
            try
            {
                genericInstance = sourceClass.CreateInstance();
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
            {
                // rethrow with actual class name we tried
                throw new Exception($"Couldn't find management class {sourceClass}", ex);
            }

            foreach (PropertyInfo propertyInfo in obj.GetType().GetProperties())
            {
                WMIIgnore ignoreProp = propertyInfo.GetCustomAttribute<WMIIgnore>();

                if (ignoreProp == null)
                {
                    WMIProperty propAtt = propertyInfo.GetCustomAttribute<WMIProperty>();

                    if (propAtt == null)
                    {
                        if (propertyInfo.PropertyType == typeof(DateTime))
                        {
                            genericInstance[propertyInfo.Name.ToUpper()] = ManagementDateTimeConverter.ToDmtfDateTime(Convert.ToDateTime(propertyInfo.GetValue(obj)));
                        }
                        else
                        {
                            genericInstance[propertyInfo.Name.ToUpper()] = propertyInfo.GetValue(obj);
                        }
                    }
                    else
                    {
                        if (propertyInfo.PropertyType == typeof(DateTime))
                        {
                            genericInstance[propAtt.Name.ToUpper()] = ManagementDateTimeConverter.ToDmtfDateTime(Convert.ToDateTime(propertyInfo.GetValue(obj)));
                        }
                        else
                        {
                            genericInstance[propAtt.Name.ToUpper()] = propertyInfo.GetValue(obj);
                        }
                    }
                }
            }

            return genericInstance;
        }

        public static WMISearchKey GetSearchKey(object p)
        {
            WMISearchKey res = null;

            foreach (PropertyInfo propertyInfo in p.GetType().GetProperties())
            {
                WMIIgnore ignoreProp = propertyInfo.GetCustomAttribute<WMIIgnore>();

                if (ignoreProp == null)
                {
                    WMIProperty propAtt = propertyInfo.GetCustomAttribute<WMIProperty>();

                    if (propAtt != null)
                    {
                        if (propAtt.SearchKey)
                        {
                            res = new WMISearchKey
                            {
                                Name = propAtt.Name,
                                Value = propertyInfo.GetValue(p)
                            };

                            break;
                        }
                    }
                }
            }

            return res;
        }

        public static List<WMISearchKey> GetSearchKeys(object p)
        {
            List<WMISearchKey> res = new List<WMISearchKey>();

            foreach (PropertyInfo propertyInfo in p.GetType().GetProperties())
            {
                WMIIgnore ignoreProp = propertyInfo.GetCustomAttribute<WMIIgnore>();

                if (ignoreProp == null)
                {
                    WMIProperty propAtt = propertyInfo.GetCustomAttribute<WMIProperty>();

                    if (propAtt != null)
                    {
                        if (propAtt.SearchKey)
                        {
                            res.Add(new WMISearchKey
                            {
                                Name = propAtt.Name,
                                Value = propertyInfo.GetValue(p)
                            });
                        }
                    }
                }
            }

            return res;
        }

        public static string GetPropertiesToSearch(Type type)
        {
            List<String> res = new List<string>();

            foreach (PropertyInfo propertyInfo in type.GetProperties())
            {
                WMIIgnore ignoreProp = propertyInfo.GetCustomAttribute<WMIIgnore>();

                if (ignoreProp == null)
                {
                    WMIProperty propAtt = propertyInfo.GetCustomAttribute<WMIProperty>();

                    if (propAtt == null)
                    {
                        res.Add(propertyInfo.Name.ToUpper());
                    }
                    else
                    {
                        res.Add(propAtt.Name.ToUpper());
                    }
                }
            }

            return String.Join(",", res);
        }
    }

    public class WMISearchKey
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }
}
