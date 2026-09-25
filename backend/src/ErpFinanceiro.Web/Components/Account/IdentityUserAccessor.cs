using ErpFinanceiro.Domain;
using Microsoft.AspNetCore.Identity;

namespace ErpFinanceiro.Web.Components.Account;

internal sealed class IdentityUserAccessor(UserManager<Usuario> userManager, IdentityRedirectManager redirectManager)
{
    public async Task<Usuario> GetRequiredUserAsync(HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            redirectManager.RedirectToWithStatus(
                "Account/Login", "Error: não foi possível carregar o usuário atual.", context);
        }

        return user;
    }
}
