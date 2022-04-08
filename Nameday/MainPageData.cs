using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Appointments;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.Email;
using Common;

namespace Nameday
{
    public class MainPageData : INotifyPropertyChanged
    {
        private string _greeting = "Hello world";

        private List<NamedayModel> _allNamedays = new List<NamedayModel>();

        public ObservableCollection<NamedayModel> Namedays { get; set; }

        public Settings Settings { get; } = new Settings();

        public MainPageData()
        {
            addReminderCommand = new AddReminderCommand(this);


            Namedays = new ObservableCollection<NamedayModel>();

           if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                Contacts = new ObservableCollection<NamedayContact>
                {
                    new NamedayContact("Contact","1"),
                    new NamedayContact("Contact","1"),
                    new NamedayContact("Contact","1"),
                };

                for (int month = 1; month <= 12; month++)
                {
                    _allNamedays.Add(new NamedayModel(
                        month, 1, new string[] { "adam" }));
                    _allNamedays.Add(new NamedayModel(
                        month, 24, new string[] { "eve", "andrew" }));
                }
                PerformFiltering();
            }

            LoadData();
        }

        public async void LoadData()
        {
            _allNamedays = await NamedayRepository.GetAllNamedaysAsync();
            PerformFiltering();
        }

        public string Greeting
        {
            get { return _greeting; }
            set
            {
                if (value == _greeting)
                    return;

                _greeting = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(Greeting)));
            }
        }

        private NamedayModel _selectedNameday;

        public event PropertyChangedEventHandler PropertyChanged;

        public NamedayModel SelectedNameday
        {
            get { return _selectedNameday; }
            set
            {
                _selectedNameday = value;

                if (value == null)
                    Greeting = "Hello World!";
                else
                    Greeting = "Hello " + value.NamesAsString;

                UpdateContacts();
                addReminderCommand.FireCanExecuteChanged();
            }
        }

        private async void UpdateContacts()
        {
            Contacts.Clear();

            if (SelectedNameday != null)
            {
                var contactStore =
                    await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);

                foreach (var name in SelectedNameday.Names)
                    foreach (var contact in await contactStore.FindContactsAsync(name))
                        Contacts.Add(new NamedayContact(contact));
            }
        }

        private string _filter;
        public string Filter
        {
            get { return _filter; }
            set
            {
                if (value == _filter)
                    return;

                _filter = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs(nameof(Filter)));

                PerformFiltering();
            }
        }

        private void PerformFiltering()
        {
            if (_filter == null)
                _filter = "";

            var lowerCaseFilter = Filter.ToLowerInvariant().Trim();

            var result =
                _allNamedays.Where(d => d.NamesAsString.ToLowerInvariant()
                .Contains(lowerCaseFilter))
                .ToList();

            var toRemove = Namedays.Except(result).ToList();

            foreach (var x in toRemove)
                Namedays.Remove(x);

            var resultCount = result.Count;
            for (int i = 0; i < resultCount; i++)
            {
                var resultItem = result[i];
                if (i + 1 > Namedays.Count || !Namedays[i].Equals(resultItem))
                    Namedays.Insert(i, resultItem);
            }
        }

        public ObservableCollection<NamedayContact> Contacts { get; } = new ObservableCollection<NamedayContact>();

        public async Task SendEmailToContact(Contact contact)
        {
            if (contact == null || contact.Emails.Count == 0)
                return;
            

            var emailMessage = new EmailMessage();
            emailMessage.To.Add(new EmailRecipient(contact.Emails[0].Address));
            emailMessage.Subject = "Happy Nameday!";

            await EmailManager.ShowComposeNewEmailAsync(emailMessage);
        }

        
        public async void AddReminderToCalendar()
        {
            Appointment appointment  = new Appointment();
            appointment.Subject = "Nameday reminder for " + SelectedNameday.NamesAsString;
            appointment.AllDay = true;
            appointment.BusyStatus = AppointmentBusyStatus.Free;
            var dateThisYear = new DateTime(DateTime.Now.Year,SelectedNameday.Month,SelectedNameday.Day);
            appointment.StartTime = dateThisYear < DateTime.Now ? dateThisYear.AddYears(1): dateThisYear;

            await AppointmentManager.ShowEditNewAppointmentAsync(appointment);
                
        }

        public AddReminderCommand addReminderCommand { get; }

        public class AddReminderCommand : ICommand
        {
            private MainPageData _mainPageData;

            public AddReminderCommand(MainPageData mainPageData)
            {
                _mainPageData = mainPageData;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter) => _mainPageData.SelectedNameday != null;

            public void Execute(object parameter) => _mainPageData.AddReminderToCalendar();

            public void FireCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

    }
}
