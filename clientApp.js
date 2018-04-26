
// Rounting
var app = angular.module('IdeaApp', ["ngRoute",'ngMaterial', 'ngMessages']);
app.config(function($routeProvider) {
    $routeProvider
    .when("/", {
        templateUrl : "userAccount.html"
    })
    .when("/draw", {
        templateUrl : "draw.html"
    })
});


// User Account Ctrl
app.controller('userAccountCtrl', function($scope, $http) {
    $scope.isloading = true;
    $scope.balance = 0;
    $scope.available = 0;
    $scope.line = 0;

    $http({
        method: 'GET',
        url: '/api/load'
      }).then(function successCallback(response) {

          // TODO: Validate "response.data"

          $scope.balance = response.data.balance;
          $scope.available = response.data.line - response.data.balance;
          $scope.line = response.data.line;
          $scope.isloading = false;
        }, function errorCallback(response) {
          alert('error'); // TODO: Notify user of error
        });

    $scope.makeADraw = function(){
        window.location = "/#!draw";
    }
});


// Draw Ctrl
app.controller('drawCtrl', function($scope, $http) {
    $scope.drawAmount = 0;

    // TODO: Validate "drawAmount"

    $scope.submitADraw = function(){
        $http.post('/api/save', { draw: $scope.drawAmount }).then(function successCallback(response) {
            window.location = "/#!";
        }, function errorCallback(response) {
            alert('error'); // TODO: Notify user of error
        });    
    }
});