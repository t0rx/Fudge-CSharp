using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Reflection;

namespace Fudge.Serialization.Reflection
{
    /// <summary>
    /// Mixin to help with calling methods that have been marked with [OnSerializing], etc.
    /// </summary>
    internal sealed class BeforeAfterSerializationMixin
    {
        private readonly StreamingContext streamingContext;
        private readonly Action<object, StreamingContext> beforeSerialize;
        private readonly Action<object, StreamingContext> afterSerialize;
        private readonly Action<object, StreamingContext> beforeDeserialize;
        private readonly Action<object, StreamingContext> afterDeserialize;

        public BeforeAfterSerializationMixin(FudgeContext context, TypeData typeData)
        {
            this.streamingContext = new StreamingContext(StreamingContextStates.Persistence, context);

            var untypedDelegateCreator = ReflectionUtil.CreateStaticMethodDelegate<Func<Type, MethodInfo[], Action<object, StreamingContext>>>(GetType(), "CreateUntypedDelegate", new Type[] { typeData.Type });

            beforeSerialize = untypedDelegateCreator(typeof(OnSerializingAttribute), typeData.AllInstanceMethods);
            afterSerialize = untypedDelegateCreator(typeof(OnSerializedAttribute), typeData.AllInstanceMethods);
            beforeDeserialize = untypedDelegateCreator(typeof(OnDeserializingAttribute), typeData.AllInstanceMethods);
            afterDeserialize = untypedDelegateCreator(typeof(OnDeserializedAttribute), typeData.AllInstanceMethods);
        }

        public void CallBeforeSerialize(object obj)
        {
            beforeSerialize(obj, streamingContext);
        }

        public void CallAfterSerialize(object obj)
        {
            afterSerialize(obj, streamingContext);
        }

        public void CallBeforeDeserialize(object obj)
        {
            beforeDeserialize(obj, streamingContext);
        }

        public void CallAfterDeserialize(object obj)
        {
            afterDeserialize(obj, streamingContext);
        }

        private static Action<object, StreamingContext> CreateUntypedDelegate<T>(Type attribType, MethodInfo[] methods)
        {
            var method = GetFirstMethodWithAttribute(attribType, methods);
            if (method == null)
                return (o, sc) => { };

            var methodDelegate = (Action<T, StreamingContext>)Delegate.CreateDelegate(typeof(Action<T, StreamingContext>), null, method);

            return (obj, sc) => { methodDelegate((T)obj, sc); };

        }

        private static MethodInfo GetFirstMethodWithAttribute(Type attribType, MethodInfo[] methods)
        {
            foreach (var method in methods)
            {
                if (method.GetCustomAttributes(attribType, true).Length > 0)
                    return method;
            }
            return null;
        }
    }
}
