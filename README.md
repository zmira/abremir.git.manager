# abremir.git.manager

A simple terminal UI app to help running git commands against multiple repositories, in bulk.

## Motivation

Did you ever find yourself in a place where you have a large number of git repositories, and want to update them all in one go, but no tool to run git commands in bulk?

That is why this small console UI app was created.

## Features

* recursively list all git repositories from a base directory
* switch to another branch, e.g. `git checkout`
* update status of existing repositories, e.g. `git fetch`
* update existing repositories to latest commits if behind, e.g. `git pull` (only if the checked-out branch is not dirty)
* reset uncommitted changes, e.g. `git reset HEAD --hard`
* view uncommitted changes, e.g. `git diff` (if checked-out branch is dirty)
* filter repository list by dirty (`*`), behind (`↓`) and/or in error

## Dependencies

* [command-line-api](https://github.com/dotnet/command-line-api)
* [Kurukuru](https://github.com/mayuki/Kurukuru)
* [libgit2sharp](https://github.com/libgit2/libgit2sharp)
* [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui)