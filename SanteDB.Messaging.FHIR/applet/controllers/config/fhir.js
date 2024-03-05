/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2023-6-21
 */
angular.module('santedb').controller('FhirConfigurationController', ['$scope', '$rootScope', function ($scope, $rootScope) {
    
    // Watch the configuration
    $scope.$parent.$watch("config", function(n, o) {
        if(n) {
            $scope.config = n;
            // Get FHIR configuration
            $scope.fhirConfig = $scope.config.others.find(function(s) {
                return s.$type === "SanteDB.Messaging.FHIR.Configuration.FhirServiceConfigurationSection, SanteDB.Messaging.FHIR";
            });

            // Get the FHIR service configuration for either embedded server or full server
            $scope.serviceConfig = $scope.config.others.find(function (s) {
                return s.$type === "SanteDB.Core.Configuration.RestConfigurationSection, SanteDB.Core" ||
                    s.$type === "SanteDB.DisconnectedClient.Ags.Configuration.AgsConfigurationSection, SanteDB.DisconnectedClient.Ags";
            });
            
        }
    });
  
}]);