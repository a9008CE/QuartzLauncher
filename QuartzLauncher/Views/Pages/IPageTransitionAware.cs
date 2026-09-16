namespace QuartzLauncher.Views.Pages;

public interface IPageTransitionAware
{
    Task PrepareForNavigationExitAsync();
}
