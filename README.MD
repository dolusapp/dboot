# Dolus Bootstrapper

Welcome to the Dolus Bootstrapper repo. This project streamlines the installation, verification, and maintenance of Dolus. It's open-source and available under the MIT license - see [LICENSE.md](LICENSE.md) for details.

## What's This All About?

Dolus Bootstrapper is a lightweight executable with no external dependencies. It handles:

- Downloading and installing the latest Dolus version
- Verifying existing installations
- Checking for and installing updates
- Uninstalling Dolus
- Self-updating
- Silent installations and uninstalls for automated deployments

The UI uses native Windows API widgets (I'll write more about this soon) for a smooth experience.

## On the Horizon

I'm working on adding:
- Rollback functionality
- Custom installation directory selection

And there's more in the pipeline.

## Why Open Source?

With Squirrel's deprecation, many developers are searching for a lightweight, maintainable alternative. Existing installer solutions often feel clunky and inflexible. I created this project to fill that gap.

Dolus Bootstrapper aims to be small, infinitely maintainable, and highly portable. It offers a streamlined approach to installation management without the overhead of more complex systems. By open-sourcing this, I'm providing a tool that meets modern deployment needs while encouraging collaboration.

## Prerequisites

You'll need .NET 8 to build this project. Make sure it's installed in your development environment.

## Building the Project

1. Clone the repo:
   ```
   git clone https://github.com/yourusername/dolus-bootstrapper.git
   ```

2. Navigate to the project directory:
   ```
   cd dolus-bootstrapper
   ```

3. Restore dependencies:
   ```
   dotnet restore
   ```

4. Build the project:
   ```
   dotnet build --configuration Release
   ```

5. Run the bootstrapper:
   ```
   dotnet run --configuration Release
   ```

## License

This project is under the MIT License. Check out [LICENSE.md](LICENSE.md) for the full text.

Feel free to dive in, and don't hesitate to reach out if you have any questions or ideas!