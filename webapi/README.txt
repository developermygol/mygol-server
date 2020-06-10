
# Support for multiple organizations

Each organization will have its own domain and database. There will be only 
one instance of the webapi per machine, serving all organizations by handling
requests to different database connections. This means that all api endpoints
must be reentrant. 

Possible issues: 
* CORS configuration must happen at Startup.cs, meaning that adding a new 
    organization requires restarting the webapi. 
    * Mitigation: Actually CORS is only needed if XHR requests are made to 
        different domains. In production, a single domain holds both
        public and private frontends as well as backend, so there is no cross
        origin going on. At some point, we may have S3 or Spaces handle the 
        uploads, but this should be only for images and static content. 
* Frontend variables can only be set at build time to a single domain   
    * Mitigation: well, they should be relative to the root of the path in 
        the URL (that is, not specifying the host part).


Steps to add a new org: 

* organizations.json
    * Add the new section with the config. 
		* Copy a section or add "OtherNames": [ "www....", "www..." ], to an existing one. 
	* Restart the backend: 
		* supervisorctl restart mygol
* mygolcli
    * setup appsettings.json for the new db or use --org="org name"
    * Create the database 
		* mygolcli createdb --org="org name" org [drop]
    * Add the admin user (level 4, last arg)
		* mygolcli createuser --org="org_name" "user_name" "user_email" "password" 4
* nginx 
    * Add the new domain entries
        * add a new server_name entry in the sites-available/default file, both in the 443 and 80 server blocks.
* Domain redirection
    * Add the needed A records pointing the mygol server or load balancer. 
	* We have to do this before configuring the certificate because letsencrypt needs to validate we are the owner. 
* nginx again
    * Setup the certificate for the new domain: 
		* certbot
		* It's interactive. 
		* Tell it what domains to use (should be ok to accept all)
		* Tell it to expand the existing certificate
		* Finally, tell it to redirect to HTTPS
	* Verify new config file (default)
		* Check there are no strange sections
		* nginx -t to validate syntax
	* Restart nginx
		* systemctl nginx restart
* Optionally, import news: 
	* mygolcli importnews export /app/datasets/upload