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
            if (concatType.parameterCount() == 1 && concatType.returnType() == String.class
                    && recipe.equals("\u0001-version.properties")) {
                MethodHandle handle = lookup.findStatic(
                    StringConcatFactory.class, "appendVersionProperties", concatType);
                return new ConstantCallSite(handle);
            }

            if (concatType.parameterCount() == 1 && concatType.returnType() == String.class
                    && recipe.equals("\u0001-version")) {
                MethodHandle handle = lookup.findStatic(
                    StringConcatFactory.class, "appendVersion", concatType);
                return new ConstantCallSite(handle);
            }

            MethodHandle handle = lookup.findStatic(
                StringConcatFactory.class,
                "concat",
                MethodType.methodType(String.class, String.class, Object[].class, Object[].class));
            handle = handle.bindTo(recipe);
            handle = handle.bindTo(constants);
            handle = handle.asCollector(Object[].class, concatType.parameterCount());
            return new ConstantCallSite(handle.asType(concatType));
        } catch (Throwable t) {
            throw new BootstrapMethodError("StringConcatFactory failure: " + t, t);
        }
    }

    public static String concat(String recipe, Object[] constants, Object[] arguments) {
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
    public static String appendVersionProperties(String value) {
        return value + "-version.properties";
    }
    public static String appendVersion(String value) {
        return value + "-version";
    }
}
