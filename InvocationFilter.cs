#pragma warning disable SKEXP0001 
using Microsoft.SemanticKernel;

public class InvocationFilter() : IAutoFunctionInvocationFilter
{
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        string functionName = context.Function.Name;
        Console.WriteLine($"Function invoked: {functionName}");


        await next(context).ConfigureAwait(false);
    }
}
#pragma warning restore SKEXP0001 
