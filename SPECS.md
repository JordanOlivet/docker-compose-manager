This repository host 2 applications, docker-compose-manager-back and docker-compose-manager-front.

These applications have to work together to provide a docker/docker-compose management.
This stack is primaraly designed to run in docker containers and so both app should be build into a docker image.
Also a compose file aggregate both services to be able to start the entire stack by running docker compose up.
The stack is cross-platform and should be executable on Windows and Linux at least.

For testing debugging the stack should be able to be run locally on the developper computer OS.

Here is the specs for those two apps.

1. docker-compose-manager-back

This is an API service application in .Net 9 and C#.
This service is in charge of interfacing the docker engine running on the host with the web app but also expose all the functionalities necessary to manager docker compose files via the wab app.
This is done by exposing REST API routes.

So the app should provide the following functionalities :

* 

2. docker-compose-manager-front

This is a web app in React.
This app should provide the following functionalities :

* User system with rights, types and restrictions that can be only managed by user of type "admin".
* Users can use docker functionalities (start, stop, restart and delete containers; docker ps; all docker compose commands) through a nice UI
* Users can see all docker compose files in a configured path and manage them (compose up, compose down, compose restart, compose start, compose stop)
* admin users can configure the app (list of paths where to manager compose files, TBD)
* Users can see a dashboard as the app main page with all importants informations about containers
* Users can manage compose files (edit compose file, create compose file, remove compose file)