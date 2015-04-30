using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Web_API;
using System.Configuration;
using System.Messaging;
using System.Text;
using System.Text.RegularExpressions;

namespace Web_API.Controllers
{
    public class PeopleController : ApiController
    {
        private CroweTestEntities db = new CroweTestEntities();

        // GET: api/People
        public IQueryable<Person> GetPeople()
        {
            return db.People;
        }

        // GET: api/People/5
        [ResponseType(typeof(Person))]
        public IHttpActionResult GetPerson(int id)
        {
            /* We will always store things in the database even if we ultimately are writing to another source
             * like a console application, so we can count on it being in the db
             */
            Person person = db.People.Find(id);
            if (person == null)
            {
                return NotFound();
            }

            return Ok(person);
        }

        private void QueueMessage(dynamic message)
        {
            // Really should be in configuration
            // We might eventually move all of this logic (database logic, message queueing, etc.) into some kind of a Strategy Pattern and use a
            // Factory Pattern to decide which algorithm to use
            const string name = @".\Private$\CroweQueue";

            MessageQueue queue = null;

            if (MessageQueue.Exists(name))
                queue = new MessageQueue(name);
            else
                queue = MessageQueue.Create(name);

            queue.Send(message);
        }

        // PUT: api/People/5
        [ResponseType(typeof(void))]
        public IHttpActionResult PutPerson(int id, Person person)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != person.Id)
            {
                return BadRequest();
            }

            try
            {
                // Eventually factor this out into a new method
                var config = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration(System.Web.HttpContext.Current.Request.ApplicationPath);
                var messageQueue = config.AppSettings.Settings["MessageQueue"];
                if (bool.Parse(messageQueue.Value))
                    QueueMessage(PersonToString(person));

                // Always write to the DB
                db.Entry(person).State = EntityState.Modified;
                db.SaveChanges();
            }
            // Eventually 
            catch (DbUpdateConcurrencyException)
            {
                if (!PersonExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        // This is actually not that great of a way of doing this, I'd prefer data annotations
        private bool IsValid(Person person)
        {
            // Email address, address, and phone number aren't technically required

            /* This isn't that great of an email address regex. It only allows .com top-level domains.
             * 
             * Email address isn't a required field, so it MAY be null but it mayn't be an empty string or whitespace (meaning it must either be
             * a valid email address or nothing).
             * 
             * It does allow optional dots and dashes in the middle of the email but not after the at sign. Examples of valid email addresses:
             * 
             * Examples of invalid email addresses:
             * 1. -test@test.com
             * 2. .test@test.com
             * 3. test@test-123.com (really should probably be valid)
             */
            if (person.EmailAddress != null && !Regex.IsMatch(person.EmailAddress, @"[A-Za-z1-9]+([\.-][A-Za-z0-9]+)*@[A-Za-z1-9]+\.[Cc][Oo][Mm]"))
                return false;

            /* Much like above, phone number isn't required, so this will either be null or a correct phone number.
             * 
             * This only handles U.S. phone numbers. There are the following components:
             * 1. Optional country code with an optional dash
             * 2. Area code - either 3 numbers or 3 numbers in parenthesis - e.g. (123)456-7890 is valid. Optional dash afterwards. (You could make the argument that (123)-456-7890 isn't
             * correct but that's a different question)
             * 3. 3 digit exchange code, optional dash
             * 4. 4-digit subscriber code
             */
            if (person.PhoneNumber != null && !Regex.IsMatch(person.PhoneNumber, @"(1-?)?([0-9]{3}|\([0-9]{3}\))-?[0-9]{3}-?[0-9]{4}"))
                return false;

            return true;
        }

        // This really should be a BL function
        private string PersonToString(Person person)
        {
            return String.Format("ID: {0}.{1}Name: {2} {3}{1}Email address: {4}{1}Phone number: {5}{1}Address: {6}", person.Id, Environment.NewLine, person.FirstName, person.LastName, person.EmailAddress, person.PhoneNumber, person.Address);
        }

        // POST: api/People
        [ResponseType(typeof(Person))]
        public IHttpActionResult PostPerson(Person person)
        {
            // Normally you'd want to do some kind of validation of this
            // You could create a separate model and business layer and use data annotation here
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Eventually move this out to a new method
            var config = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration(System.Web.HttpContext.Current.Request.ApplicationPath);
            var messageQueue = config.AppSettings.Settings["MessageQueue"];

            // Essentially, a message queue allows us to eventually write this to something like a console application if we want to
            if (bool.Parse(messageQueue.Value))
                QueueMessage(PersonToString(person)); // We could've just sent the whole object if we had chosen

            // Always add to the db
            db.People.Add(person);
            db.SaveChanges();

            return CreatedAtRoute("DefaultApi", new { id = person.Id }, person);
        }

        // DELETE: api/People/5
        [ResponseType(typeof(Person))]
        public IHttpActionResult DeletePerson(int id)
        {
            Person person = db.People.Find(id);
            if (person == null)
            {
                return NotFound();
            }

            db.People.Remove(person);
            db.SaveChanges();

            return Ok(person);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool PersonExists(int id)
        {
            return db.People.FirstOrDefault(e => e.Id == id) != null;
            //return db.People.Count(e => e.Id == id) > 0;
        }
    }
}