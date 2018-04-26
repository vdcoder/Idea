const express = require('express')
const app = express()

var mongojs = require('mongojs')
var db = mongojs('idea')
const bodyParser = require('body-parser');

app.use(bodyParser.urlencoded({ extended: false }));
app.use(bodyParser.json())

app.get('/api/load', (req, res) => 
{
    // User account id hardcoded to "1234"

    let collection = db.collection('userAccounts');
    collection.findOne({ user_id: 1234 }, function(err, doc) {
        res.send(doc)

        // TODO: error checking
    })    
})

app.post('/api/save', (req, res) => 
{
    // User account id hardcoded to "1234"

    let collection = db.collection('userAccounts');
    collection.findOne({ user_id: 1234 }, function (err, doc, lastErrorObject) {

        // TODO: error checking
        
        collection.findAndModify({
            query: { user_id: 1234 },
            update: { $set: { balance: (parseInt(doc.balance) + parseInt(req.body.draw)) } },
            new: true
        }, function (err2, doc2, lastErrorObject2) {
            res.send(true)

            // TODO: error checking
        })
    })
})

app.use(express.static('public'))

app.listen(3000, () => console.log('Example app listening on port 3000!'))