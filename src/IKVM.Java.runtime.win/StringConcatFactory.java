/*
 * Java 9 introduced invokedynamic string concatenation. The JDK 8 source
 * tree used to build IKVM's runtime does not provide this bootstrap class.
 */
package java.lang.invoke;

public final class StringConcatFactory {
    private static final char ARGUMENT = '\u0001';
    private static final char CONSTANT = '\u0002';

    private StringConcatFactory() { }

    public static CallSite makeConcat(MethodHandles.Lookup lookup, String name,
            MethodType concatType) throws Throwable {
        StringBuilder recipe = new StringBuilder(concatType.parameterCount());
        for (int i = 0; i < concatType.parameterCount(); i++)
            recipe.append(ARGUMENT);
        return makeConcatWithConstants(lookup, name, concatType,
            recipe.toString(), new Object[0]);
    }

    public static CallSite makeConcatWithConstants(MethodHandles.Lookup lookup,
            String name, MethodType concatType, String recipe, Object... constants)
            throws Throwable {
        try {
            if (concatType.returnType() == String.class
                    && concatType.parameterCount() == 1
                    && concatType.parameterType(0) == String.class) {
                MethodHandle handle = MethodHandles.lookup().findStatic(
                    StringConcatFactory.class,
                    "concatString",
                    MethodType.methodType(String.class, String.class, Object[].class, String.class));
                return new ConstantCallSite(handle.bindTo(recipe).bindTo(constants));
            }

            MethodHandle handle = MethodHandles.lookup().findStatic(
                StringConcatFactory.class,
                "concat",
                MethodType.methodType(String.class, String.class, Object[].class, Object[].class));
            handle = handle.bindTo(recipe);
            handle = handle.bindTo(constants);
            handle = handle.asCollector(Object[].class, concatType.parameterCount());
            return new ConstantCallSite(handle.asType(concatType));
        } catch (Throwable t) {
            t.printStackTrace();
            throw t;
        }
    }

    private static String concat(String recipe, Object[] constants, Object[] arguments) {
        StringBuilder value = new StringBuilder();
        int argumentIndex = 0;
        int constantIndex = 0;
        for (int i = 0; i < recipe.length(); i++) {
            char c = recipe.charAt(i);
            if (c == ARGUMENT)
                value.append(arguments[argumentIndex++]);
            else if (c == CONSTANT)
                value.append(constants[constantIndex++]);
            else
                value.append(c);
        }
        return value.toString();
    }
    private static String concatString(String recipe, Object[] constants, String argument) {
        return concat(recipe, constants, new Object[] { argument });
    }
}
