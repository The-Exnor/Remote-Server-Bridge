# Remote Server Bridge

Plugin for Jellyfin server that allows connection from a local server to a remote Jellyfin server via Username and Password (no API required)

How to use:
            Create a folder named RemoteServerBridge on your plugins folder.
            (Example: /var/lib/jellyfin/plugins/RemoteServerBridge)

            Copy the files RemoteServerBridge.dll and meta.xml to that folder.
            (if you use Linux/Unix use chown of the files to jellyfin:jellyfin)

            Restart your local Jellyfin server.
            You now should have a new Plugin named Remote Server Brige 1.0.0.0 in the installed Plugins area/page.

            Select the plugin and go to settings. Enter your remote server address (https only), username and password and clik save.

            Now go to Scheduled Tasks and search for "Remote Server Bridge Sync".  Click the run arrow and wait. 

            In the Libraries section, a new librarie called "Remote Server" should be availabe.
            Thats it. You now should be able to browse and play from the remote server you entered.
