const http = require('http');
const url = require('url');
const fs = require('fs');
const path = require('path');
const port = process.argv[2] || 9000;

http.createServer(function (req, res) {
  console.log(`${req.method} ${req.url}`);

  // parse URL
  const url_parse = url.parse(req.url);
  // extract URL path
  let pathname = `.${url_parse.pathname}`;
  var path_parse = path.parse(pathname);
  const ext = path_parse.ext;
  // maps file extention to MIME typere
  const map = {
    '.ico': 'image/x-icon',
    '.html': 'text/html',
    '.js': 'text/javascript',
    '.json': 'application/json',
    '.css': 'text/css',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.wav': 'audio/wav',
    '.mp3': 'audio/mpeg',
    '.svg': 'image/svg+xml',
    '.pdf': 'application/pdf',
    '.doc': 'application/msword'
  };

  if (ext == ".a") // action
  {
      var postAction = "";
      var postParams = {};

      if (path_parse.name == "open")
      {
          console.log("action " + path_parse.name);
          var sFullFileName = process.cwd() + "/" + url_parse.query;
          console.log(sFullFileName);
          fs.exists(sFullFileName, function (exist) {
              if(!exist) 
              {
                  res.statusCode = 404;
                  res.end(`NOT_FOUND`);
                  return;
              }
              fs.readFile(sFullFileName, function(err, data) {
                  if(err){
                      res.statusCode = 500;
                      res.end(`Error getting the file.`);
                  } else {
                      res.setHeader('Content-type', map[ext] || 'text/plain' );
                      res.end(data);
                  }
              });
          });
      }
      else if (path_parse.name == "new")
      {
          console.log("action " + path_parse.name);
          var sFullFileName = process.cwd() + "/" + url_parse.query;
          console.log(sFullFileName);
          fs.exists(sFullFileName, function (exist) {
              if(exist) 
              {
                  res.statusCode = 409;
                  res.end(`EXISTS`);
                  return;
              }
              else
              {
                  fs.writeFile(sFullFileName, '', 'utf8', function(err) {
                      if (err) return console.log(err);
                      console.log('File created successfully.');
                      res.setHeader('Content-type', map[ext] || 'text/plain' );
                      res.end('');
                  });
              }
          });
      }
      else if (path_parse.name == "save")
      {
          console.log("action " + path_parse.name);
          var sFullFileName = process.cwd() + "/" + url_parse.query;

          postAction = "save";
          postParams.sFullFileName = sFullFileName;
      }
      else if (path_parse.name == "import")
      {
          console.log("action " + path_parse.name);
          var sFullFileName = process.cwd() + "/" + url_parse.query;
          console.log(sFullFileName);
          fs.exists(sFullFileName, function (exist) {
              if(!exist) 
              {
                  res.statusCode = 404;
                  res.end(`NOT_FOUND`);
                  return;
              }
              fs.readFile(sFullFileName, function(err, data) {
                  if(err){
                      res.statusCode = 500;
                      res.end(`Error getting the file.`);
                  } else {
                      res.setHeader('Content-type', map[ext] || 'text/plain' );
                      res.end(data);
                  }
              });
          });
      }
      else
          console.log("unknown action " + path_parse.name);

      var dataPosted = "";
      if (req.method == "POST")
      {
          const chunks = [];
          req.on('data', d => dataPosted += d);
          req.on('end', ()=> {
              console.log(dataPosted);
              if (postAction == "save")
              {
                  fs.writeFile(postParams.sFullFileName, dataPosted, 'utf8', function(err) {
                      if(err) return console.log(err);
                      console.log("File saved successfully.");
                      res.setHeader('Content-type', map[ext] || 'text/plain' );
                      res.end('SAVED');
                  }); 
              }
          });
      }
  }
  else 
  {
      fs.exists(pathname, function (exist) {
        if(!exist) {
          // if the file is not found, return 404
          res.statusCode = 404;
          res.end(`File ${pathname} not found!`);
          return;
        }

        // if is a directory search for index file matching the extention
        if (fs.statSync(pathname).isDirectory()) pathname += '/index' + (ext || '.html');

        // read file from file system
        fs.readFile(pathname, function(err, data){
          if(err){
            res.statusCode = 500;
            res.end(`Error getting the file: ${err}.`);
          } else {
            // if the file is found, set Content-type and send data
            res.setHeader('Content-type', map[ext] || 'text/plain' );
            res.end(data);
          }
        });
      });
  }
}).listen(parseInt(port));

console.log(`Server listening on port ${port}`);
