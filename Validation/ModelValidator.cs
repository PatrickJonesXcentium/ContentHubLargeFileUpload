using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ContentHubLargeFileUpload.Validation
{
    /// <summary>
    /// Validates model class properties decorated with validation attributes.
    /// </summary>
    /// <typeparam name="T">Model class type to validate</typeparam>
    public class ModelValidator<T> : IModelValidation<T> where T : class
    {
        /// <summary>
        /// Performs validation of model instance by returning true or false. If false, validation results are returned in a collection.
        /// </summary>
        /// <param name="modelInstance"></param>
        /// <returns>bool Valid, ICollection<ValidationResult> Errors</returns>
        public (bool Valid, ICollection<ValidationResult> Errors) ValidateModel(T modelInstance)
        {
            // null instances fail
            if (modelInstance == null)
            {
                var typ = typeof(T).Name;
                return (false, new List<ValidationResult> { new ValidationResult($"Model instance {typ} is null.") });
            }

            try
            {
                // collection to contain validation failures
                var valFailures = new List<ValidationResult>();
                // perform model validation
                bool valResult = Validator.TryValidateObject(
                    modelInstance,
                    new ValidationContext(modelInstance),
                    valFailures,
                    true);

                return (valResult, valFailures);
            }
            catch (Exception e)
            {
                return (false, new List<ValidationResult> { new ValidationResult($"Model Validation Exception: {e.Message}") });
            }
        }
    }
}
